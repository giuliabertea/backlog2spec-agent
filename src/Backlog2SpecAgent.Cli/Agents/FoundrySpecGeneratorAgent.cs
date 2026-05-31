using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backlog2SpecAgent.Cli.Infrastructure.AI;
using Backlog2SpecAgent.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Backlog2SpecAgent.Cli.Agents;

public sealed class FoundrySpecGeneratorAgent : ISpecGeneratorAgent
{
    private const int MaxRetries = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IFoundryAgentClient _agentClient;
    private readonly HttpClient _toolsHttp;
    private readonly string _toolsBaseUrl;
    private readonly ILogger<FoundrySpecGeneratorAgent> _logger;

    public FoundrySpecGeneratorAgent(
        IFoundryAgentClient agentClient,
        string toolsBaseUrl,
        string toolsApiKey,
        ILogger<FoundrySpecGeneratorAgent> logger)
    {
        _agentClient = agentClient;
        _toolsBaseUrl = toolsBaseUrl.TrimEnd('/');
        _toolsHttp = new HttpClient();
        _toolsHttp.DefaultRequestHeaders.Add("X-Api-Key", toolsApiKey);
        _logger = logger;
    }

    public async Task<GeneratedSpec> GenerateAsync(int workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Foundry Agent spec generation for work item {WorkItemId}", workItemId);

        // a. Fetch work item from Tools API
        var workItemJson = await FetchWorkItemAsync(workItemId, ct);

        // b. Extract title and fetch relevant repo context
        var title = ExtractTitle(workItemJson);
        var repoContextJson = await FetchRepoContextAsync(title, ct);

        // c. Build combined payload for the Foundry agent
        var payload = BuildPayload(workItemJson, repoContextJson);

        // d+e. Call the agent and parse the response, retrying on JSON errors
        string lastRaw = string.Empty;
        JsonException? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            lastRaw = await _agentClient.RunAsync(payload, ct);
            _logger.LogDebug("Foundry Agent response size: {Chars} chars (attempt {Attempt})", lastRaw.Length, attempt + 1);

            try
            {
                var json = ExtractJson(lastRaw);
                var foundrySpec = JsonSerializer.Deserialize<FoundrySpec>(json, JsonOptions);
                if (foundrySpec is not null)
                {
                    _logger.LogInformation("Foundry Agent spec generation completed for work item {WorkItemId}", workItemId);
                    return MapToGeneratedSpec(foundrySpec);
                }
            }
            catch (JsonException ex)
            {
                lastException = ex;
                _logger.LogDebug("Foundry Agent attempt {Attempt} returned invalid JSON", attempt + 1);
            }
        }

        throw new LlmFormatException(lastRaw, lastException);
    }

    private async Task<string> FetchWorkItemAsync(int workItemId, CancellationToken ct)
    {
        var resp = await _toolsHttp.GetAsync($"{_toolsBaseUrl}/workitem/{workItemId}", ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Tools API GET /workitem/{workItemId} → {(int)resp.StatusCode}: {body}");
        return body;
    }

    private async Task<string> FetchRepoContextAsync(string query, CancellationToken ct)
    {
        var bodyJson = JsonSerializer.Serialize(new { query });
        using var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        var resp = await _toolsHttp.PostAsync($"{_toolsBaseUrl}/repo-context", content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            _logger.LogWarning("Tools API POST /repo-context → {Status}: {Body}", (int)resp.StatusCode, body);
        return body;
    }

    private static string ExtractTitle(string workItemJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(workItemJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("title", out var t) || root.TryGetProperty("Title", out t))
                return t.GetString() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    private static string BuildPayload(string workItemJson, string repoContextJson)
    {
        JsonElement workItem;
        using (var doc = JsonDocument.Parse(workItemJson))
            workItem = doc.RootElement.Clone();

        JsonElement? repoContext = null;
        if (!string.IsNullOrWhiteSpace(repoContextJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(repoContextJson);
                repoContext = doc.RootElement.Clone();
            }
            catch { }
        }

        return JsonSerializer.Serialize(new
        {
            workItem,
            projectConfig = new { stack = ".NET 8, Clean Architecture" },
            repoContext
        });
    }

    private static GeneratedSpec MapToGeneratedSpec(FoundrySpec foundrySpec) =>
        new()
        {
            Goal = foundrySpec.Goal,
            Behaviour = foundrySpec.Behaviour,
            EdgeCases = foundrySpec.EdgeCases,
            OutOfScope = string.Join(", ", foundrySpec.OutOfScope),
            FilesToChange = foundrySpec.FilesToChange
                .Select(f => $"{f.File}: {f.Change}")
                .ToList()
        };

    private static string ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : raw;
    }

    // Matches the schema the Foundry Agent is configured to return (see README for portal setup).
    private sealed class FoundrySpec
    {
        [JsonPropertyName("goal")]
        public string Goal { get; init; } = string.Empty;

        [JsonPropertyName("behaviour")]
        public List<string> Behaviour { get; init; } = [];

        [JsonPropertyName("edgeCases")]
        public List<string> EdgeCases { get; init; } = [];

        [JsonPropertyName("outOfScope")]
        public List<string> OutOfScope { get; init; } = [];

        [JsonPropertyName("filesToChange")]
        public List<FoundryFileChange> FilesToChange { get; init; } = [];
    }

    private sealed class FoundryFileChange
    {
        [JsonPropertyName("file")]
        public string File { get; init; } = string.Empty;

        [JsonPropertyName("change")]
        public string Change { get; init; } = string.Empty;
    }
}
