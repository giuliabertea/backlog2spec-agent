using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backlog2SpecAgent.Cli.Config;
using Backlog2SpecAgent.Cli.Infrastructure.AI;
using Backlog2SpecAgent.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Backlog2SpecAgent.Cli.Agents;

public sealed class FoundrySpecGeneratorAgent : ISpecGeneratorAgent
{
    private const int MaxRetries = 2;
    private const string SearchApiVersion = "2023-11-01";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IFoundryAgentClient _agentClient;
    private readonly ConfigLoader _configLoader;
    private readonly HttpClient _toolsHttp;
    private readonly string _toolsBaseUrl;
    private readonly HttpClient _searchHttp;
    private readonly string _searchEndpoint;
    private readonly string _searchIndexName;
    private readonly ILogger<FoundrySpecGeneratorAgent> _logger;

    public FoundrySpecGeneratorAgent(
        IFoundryAgentClient agentClient,
        ConfigLoader configLoader,
        string toolsBaseUrl,
        string toolsApiKey,
        string searchEndpoint,
        string searchApiKey,
        string searchIndexName,
        ILogger<FoundrySpecGeneratorAgent> logger)
    {
        _agentClient = agentClient;
        _configLoader = configLoader;
        _toolsBaseUrl = toolsBaseUrl.TrimEnd('/');
        _toolsHttp = new HttpClient();
        _toolsHttp.DefaultRequestHeaders.Add("X-Api-Key", toolsApiKey);
        _searchEndpoint = searchEndpoint.TrimEnd('/');
        _searchIndexName = searchIndexName;
        _searchHttp = new HttpClient();
        _searchHttp.DefaultRequestHeaders.Add("api-key", searchApiKey);
        _logger = logger;
    }

    public async Task<GeneratedSpec> GenerateAsync(int workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Foundry Agent spec generation for work item {WorkItemId}", workItemId);

        // a. Fetch work item from Tools API
        var workItemJson = await FetchWorkItemAsync(workItemId, ct);

        // b. Extract title and query Azure AI Search for relevant code snippets
        var title = ExtractTitle(workItemJson);
        var repoContext = await QueryAzureSearchAsync(title, ct);

        // c. Load project config (graceful fallback to defaults if no config file found)
        var config = await _configLoader.LoadAsync(ct);

        // d. Build combined payload for the Foundry agent
        var payload = BuildPayload(workItemJson, repoContext, config);

        // e+f. Call the agent and parse the response, retrying on JSON errors
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

    private async Task<List<RepoContextItem>> QueryAzureSearchAsync(string query, CancellationToken ct)
    {
        var url = $"{_searchEndpoint}/indexes/{_searchIndexName}/docs/search?api-version={SearchApiVersion}";
        var bodyJson = JsonSerializer.Serialize(new { search = query, select = "filePath,content", top = 5 });
        using var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        HttpResponseMessage resp;
        try
        {
            resp = await _searchHttp.PostAsync(url, content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure AI Search request failed — continuing without repo context");
            return [];
        }

        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Azure AI Search POST /docs/search → {Status}: {Body} — continuing without repo context",
                (int)resp.StatusCode, body);
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("value", out var values))
                return [];

            var results = new List<RepoContextItem>();
            foreach (var item in values.EnumerateArray())
            {
                var filePath = item.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? string.Empty : string.Empty;
                var itemContent = item.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : string.Empty;
                if (!string.IsNullOrWhiteSpace(filePath))
                    results.Add(new RepoContextItem(filePath, itemContent));
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Azure AI Search response — continuing without repo context");
            return [];
        }
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

    private static string BuildPayload(string workItemJson, IReadOnlyList<RepoContextItem> repoContext, AgentConfig config)
    {
        using var doc = JsonDocument.Parse(workItemJson);
        var workItem = doc.RootElement.Clone();

        var mapped = repoContext.Select(r => new { file = r.File, content = r.Content }).ToArray();
        var projectConfig = new
        {
            stack = BuildStack(config),
            architecture = config.Project.Architecture,
            conventions = BuildConventions(config)
        };
        var repoContextValue = mapped.Length > 0 ? (object?)mapped : null;

        if (!string.IsNullOrWhiteSpace(config.DevRulesContent))
        {
            return JsonSerializer.Serialize(new
            {
                workItem,
                projectConfig,
                devRules = config.DevRulesContent,
                repoContext = repoContextValue
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            workItem,
            projectConfig,
            repoContext = repoContextValue
        }, JsonOptions);
    }

    private static string BuildStack(AgentConfig config)
    {
        var parts = new[] { config.Project.Language, config.Project.Framework, config.Project.Architecture }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        return parts.Length > 0 ? string.Join(", ", parts) : ".NET 8, Clean Architecture";
    }

    private static string BuildConventions(AgentConfig config)
    {
        var parts = new[]
            {
                config.Conventions.Naming,
                config.Conventions.DiPattern,
                config.Conventions.FolderStructure,
                config.Conventions.SpecStyle
            }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        return string.Join("; ", parts);
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

    private sealed record RepoContextItem(string File, string Content);

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
