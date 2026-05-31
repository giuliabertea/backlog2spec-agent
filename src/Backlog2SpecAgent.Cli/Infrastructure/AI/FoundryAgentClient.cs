using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Backlog2SpecAgent.Cli.Infrastructure.AI;

// Sends a message to an Azure OpenAI Assistant via the classic Assistants API (threads + runs + polling).
// Endpoint: https://<resource>.openai.azure.com/openai
// Auth: api-key header
// API version: 2024-05-01-preview
// TODO Phase 3: RAG — enrich the input with knowledge search results before calling.
public sealed class FoundryAgentClient : IFoundryAgentClient
{
    private const string ApiVersion = "2024-05-01-preview";

    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _agentId;
    private readonly HttpClient _http;
    private readonly ILogger<FoundryAgentClient> _logger;

    public FoundryAgentClient(
        string endpoint,
        string apiKey,
        string agentId,
        ILogger<FoundryAgentClient> logger)
    {
        _baseUrl = endpoint.TrimEnd('/');
        _apiKey = apiKey;
        _agentId = agentId;
        _http = new HttpClient();
        _logger = logger;

        _logger.LogWarning("FoundryAgentClient initialised — endpoint: {Endpoint}, agentId: {AgentId}",
            _baseUrl, _agentId);
    }

    public async Task<string> RunAsync(string userMessage, CancellationToken ct = default)
    {
        var threadId = await CreateThreadAsync(ct);
        _logger.LogDebug("Created thread {ThreadId}", threadId);

        try
        {
            await AddMessageAsync(threadId, userMessage, ct);

            var runId = await CreateRunAsync(threadId, ct);
            _logger.LogDebug("Created run {RunId} on thread {ThreadId}", runId, threadId);

            await PollUntilCompleteAsync(threadId, runId, ct);

            return await GetAssistantMessageAsync(threadId, ct);
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
        return doc.RootElement.GetProperty("id").GetString()!;
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
        var payload = JsonSerializer.Serialize(new { assistant_id = _agentId });
        using var req = BuildRequest(HttpMethod.Post, $"threads/{threadId}/runs");
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"POST /threads/{threadId}/runs → {(int)resp.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private async Task PollUntilCompleteAsync(string threadId, string runId, CancellationToken ct)
    {
        while (true)
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

                throw new InvalidOperationException($"Run {runId} ended with status '{status}': {errorMsg}");
            }
        }
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
            using var req = BuildRequest(HttpMethod.Delete, $"threads/{threadId}");
            await _http.SendAsync(req);
            _logger.LogDebug("Deleted thread {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete thread {ThreadId}", threadId);
        }
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path) =>
        new(method, $"{_baseUrl}/{path}?api-version={ApiVersion}")
        {
            Headers = { { "api-key", _apiKey } }
        };
}
