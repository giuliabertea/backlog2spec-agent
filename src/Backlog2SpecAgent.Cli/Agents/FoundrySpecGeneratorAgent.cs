using System.Text.Json;
using System.Text.Json.Serialization;
using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Config;
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

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IFoundryAgentClient _agentClient;
    private readonly ILogger<FoundrySpecGeneratorAgent> _logger;

    public FoundrySpecGeneratorAgent(IFoundryAgentClient agentClient, ILogger<FoundrySpecGeneratorAgent> logger)
    {
        _agentClient = agentClient;
        _logger = logger;
    }

    public async Task<GeneratedSpec> GenerateAsync(
        EnrichedTicket enriched,
        AgentConfig config,
        IReadOnlyList<CodeFileDto> codebaseContext,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Foundry Agent spec generation for work item {WorkItemId}", enriched.WorkItemId);

        var userMessage = BuildPayload(enriched, config, codebaseContext);

        string lastRaw = string.Empty;
        JsonException? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            lastRaw = await _agentClient.RunAsync(userMessage, ct);
            _logger.LogDebug("Foundry Agent response size: {Chars} chars (attempt {Attempt})", lastRaw.Length, attempt + 1);

            try
            {
                var json = ExtractJson(lastRaw);
                var foundrySpec = JsonSerializer.Deserialize<FoundrySpec>(json, JsonOptions);
                if (foundrySpec is not null)
                {
                    _logger.LogInformation("Foundry Agent spec generation completed for work item {WorkItemId}", enriched.WorkItemId);
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

    private static string BuildPayload(EnrichedTicket enriched, AgentConfig config, IReadOnlyList<CodeFileDto> codebaseContext)
    {
        var payload = new
        {
            workItem = new
            {
                id = enriched.WorkItemId,
                title = enriched.Title,
                missingAcceptanceCriteria = enriched.MissingAcceptanceCriteria,
                edgeCases = enriched.EdgeCases,
                constraints = enriched.Constraints,
                affectedComponents = enriched.AffectedComponents,
                ambiguities = enriched.Ambiguities
            },
            projectConfig = new
            {
                name = config.Project.Name,
                language = config.Project.Language,
                framework = config.Project.Framework,
                architecture = config.Project.Architecture,
                testFramework = config.Project.TestFramework,
                conventions = new
                {
                    naming = config.Conventions.Naming,
                    specStyle = config.Conventions.SpecStyle,
                    diPattern = config.Conventions.DiPattern
                }
            },
            devRules = config.DevRulesContent ?? string.Empty,
            repoContext = codebaseContext.Select(f => new { path = f.Path, content = f.Content }).ToArray()
        };

        return JsonSerializer.Serialize(payload, PayloadOptions);
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
