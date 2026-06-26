using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Backlog2SpecAgent.Cli.Infrastructure.AI;

// Sends a message to an Azure OpenAI Assistant via the classic Assistants API (threads + runs + polling).
// Endpoint: https://<resource>.openai.azure.com/openai
// Auth: api-key header
// API version: 2024-05-01-preview
public sealed class AssistantClient : IAssistantClient
{
    private const string ApiVersion = "2024-05-01-preview";
    private const int MaxRateLimitRetries = 3;
    private const int RateLimitRetryFallbackSeconds = 60;
    private const int MaxPollCount = 150; // 5 minutes at 2s per poll
    private const int CleanupTimeoutSeconds = 5;

    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _assistantId;
    private readonly HttpClient _http;
    private readonly ILogger<AssistantClient> _logger;

    public AssistantClient(
        string endpoint,
        string apiKey,
        string assistantId,
        ILogger<AssistantClient> logger)
    {
        _baseUrl = NormalizeEndpoint(endpoint);
        _apiKey = apiKey;
        _assistantId = assistantId;
        _http = new HttpClient();
        _logger = logger;

        _logger.LogWarning("AssistantClient initialised — endpoint: {Endpoint}, assistantId: {AssistantId}",
            _baseUrl, _assistantId);
    }

    // The Azure OpenAI Assistants data plane lives under /openai.
    // A bare endpoint (e.g. https://<resource>.cognitiveservices.azure.com) resolves
    // POST /threads and returns 404 "Resource not found".
    // Appending /openai when missing means either endpoint form works.
    private static string NormalizeEndpoint(string endpoint)
    {
        var trimmed = (endpoint ?? string.Empty).TrimEnd('/');
        if (!trimmed.EndsWith("/openai", StringComparison.OrdinalIgnoreCase))
            trimmed += "/openai";
        return trimmed;
    }

    public async Task<string> RunAsync(string userMessage, CancellationToken ct = default)
    {
        var threadId = await CreateThreadAsync(ct);
        _logger.LogDebug("Created thread {ThreadId}", threadId);

        try
        {
            await AddMessageAsync(threadId, userMessage, ct);

            for (var attempt = 1; ; attempt++)
            {
                var runId = await CreateRunAsync(threadId, ct);
                _logger.LogDebug("Created run {RunId} on thread {ThreadId} (attempt {Attempt})", runId, threadId, attempt);

                try
                {
                    await PollUntilCompleteAsync(threadId, runId, ct);
                    return await GetAssistantMessageAsync(threadId, ct);
                }
                catch (RateLimitException ex) when (attempt <= MaxRateLimitRetries)
                {
                    _logger.LogWarning(
                        "Run {RunId} hit rate limit (attempt {Attempt}/{Max}). Waiting {Seconds}s before retry.",
                        runId, attempt, MaxRateLimitRetries, ex.RetryAfterSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(ex.RetryAfterSeconds), ct);
                }
            }
        }
        finally
        {
            await DeleteThreadAsync(threadId);
        }
    }

    private async Task<string> CreateThreadAsync(CancellationToken ct)
    {
        using var req = BuildRequest(HttpMethod.Post, "threads");
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"POST /threads → {(int)resp.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetString()
               ?? throw new InvalidOperationException("POST /threads returned a null thread id.");
    }

    private async Task AddMessageAsync(string threadId, string userMessage, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { role = "user", content = userMessage });
        using var req = BuildRequest(HttpMethod.Post, $"threads/{threadId}/messages");
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"POST /threads/{threadId}/messages → {(int)resp.StatusCode}: {body}");
    }

    private async Task<string> CreateRunAsync(string threadId, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { assistant_id = _assistantId });
        using var req = BuildRequest(HttpMethod.Post, $"threads/{threadId}/runs");
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"POST /threads/{threadId}/runs → {(int)resp.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetString()
               ?? throw new InvalidOperationException($"POST /threads/{threadId}/runs returned a null run id.");
    }

    private async Task PollUntilCompleteAsync(string threadId, string runId, CancellationToken ct)
    {
        for (var poll = 0; poll < MaxPollCount; poll++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);

            using var req = BuildRequest(HttpMethod.Get, $"threads/{threadId}/runs/{runId}");
            var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"GET /threads/{threadId}/runs/{runId} → {(int)resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.GetProperty("status").GetString();
            _logger.LogDebug("Run {RunId} status: {Status}", runId, status);

            if (status == "completed") return;
            if (status is "failed" or "cancelled" or "expired")
            {
                var errorMsg = string.Empty;
                if (doc.RootElement.TryGetProperty("last_error", out var lastError) &&
                    lastError.ValueKind != JsonValueKind.Null &&
                    lastError.TryGetProperty("message", out var msg))
                    errorMsg = msg.GetString() ?? string.Empty;

                if (IsRateLimitError(errorMsg))
                    throw new RateLimitException(ExtractRetryAfterSeconds(errorMsg), errorMsg);

                throw new InvalidOperationException($"Run {runId} ended with status '{status}': {errorMsg}");
            }
        }

        throw new InvalidOperationException(
            $"Run {runId} did not complete within {MaxPollCount * 2} seconds.");
    }

    private async Task<string> GetAssistantMessageAsync(string threadId, CancellationToken ct)
    {
        using var req = BuildRequest(HttpMethod.Get, $"threads/{threadId}/messages");
        var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"GET /threads/{threadId}/messages → {(int)resp.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return string.Empty;

        foreach (var msg in data.EnumerateArray())
        {
            if (!msg.TryGetProperty("role", out var role) || role.GetString() != "assistant") continue;
            if (!msg.TryGetProperty("content", out var content)) continue;
            foreach (var part in content.EnumerateArray())
            {
                if (!part.TryGetProperty("type", out var type) || type.GetString() != "text") continue;
                if (part.TryGetProperty("text", out var textObj) &&
                    textObj.TryGetProperty("value", out var textValue))
                    return textValue.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private async Task DeleteThreadAsync(string threadId)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupTimeoutSeconds));
            using var req = BuildRequest(HttpMethod.Delete, $"threads/{threadId}");
            await _http.SendAsync(req, cts.Token);
            _logger.LogDebug("Deleted thread {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete thread {ThreadId}", threadId);
        }
    }

    private static bool IsRateLimitError(string message)
    {
        var lower = message.ToLowerInvariant();
        return lower.Contains("token rate limit") || lower.Contains("rate limit");
    }

    private static int ExtractRetryAfterSeconds(string message)
    {
        var match = Regex.Match(message, @"\b(\d+)\s*seconds?\b", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : RateLimitRetryFallbackSeconds;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path) =>
        new(method, $"{_baseUrl}/{path}?api-version={ApiVersion}")
        {
            Headers = { { "api-key", _apiKey } }
        };

    private sealed class RateLimitException(int retryAfterSeconds, string message)
        : Exception(message)
    {
        public int RetryAfterSeconds { get; } = retryAfterSeconds;
    }
}
