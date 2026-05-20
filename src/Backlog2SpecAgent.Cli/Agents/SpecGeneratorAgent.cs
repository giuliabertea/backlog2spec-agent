using System.Text.Json;
using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Config;
using Backlog2SpecAgent.Cli.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Backlog2SpecAgent.Cli.Agents;

public sealed class SpecGeneratorAgent : ISpecGeneratorAgent
{
    private const int MaxRetries = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly Microsoft.SemanticKernel.Kernel _kernel;
    private readonly IAdoClient _adoClient;
    private readonly IEnrichmentAgent _enrichmentAgent;
    private readonly ICodebaseContextAgent _codebaseContextAgent;
    private readonly ConfigLoader _configLoader;
    private readonly ILogger<SpecGeneratorAgent> _logger;
    private readonly string _promptTemplate;

    public SpecGeneratorAgent(
        Microsoft.SemanticKernel.Kernel kernel,
        IAdoClient adoClient,
        IEnrichmentAgent enrichmentAgent,
        ICodebaseContextAgent codebaseContextAgent,
        ConfigLoader configLoader,
        ILogger<SpecGeneratorAgent> logger)
    {
        _kernel = kernel;
        _adoClient = adoClient;
        _enrichmentAgent = enrichmentAgent;
        _codebaseContextAgent = codebaseContextAgent;
        _configLoader = configLoader;
        _logger = logger;
        _promptTemplate = LoadPrompt();
    }

    public async Task<GeneratedSpec> GenerateAsync(int workItemId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting spec generation for work item {WorkItemId}", workItemId);

        var config = await _configLoader.LoadAsync(ct);
        var workItem = await _adoClient.GetWorkItemAsync(workItemId, ct);
        var codebaseContext = await _codebaseContextAgent.FetchRelevantFilesAsync(workItem, config, ct);
        var enriched = await _enrichmentAgent.EnrichAsync(workItem, config, codebaseContext, ct);

        var prompt = BuildPrompt(enriched, config, codebaseContext);
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        string lastRaw = string.Empty;
        JsonException? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var chat = new ChatHistory();
            chat.AddUserMessage(prompt);

            var response = await chatService.GetChatMessageContentAsync(
                chat,
                new PromptExecutionSettings
                {
                    ExtensionData = new Dictionary<string, object>
                    {
                        ["temperature"] = 0.1
                    }
                },
                _kernel,
                ct);

            lastRaw = response.Content ?? string.Empty;
            _logger.LogDebug("Spec generation response size: {Chars} chars (attempt {Attempt})", lastRaw.Length, attempt + 1);

            try
            {
                var json = ExtractJson(lastRaw);
                var result = JsonSerializer.Deserialize<GeneratedSpec>(json, JsonOptions);
                if (result is not null)
                {
                    _logger.LogInformation("Spec generation completed for work item {WorkItemId}", workItemId);
                    return result;
                }
            }
            catch (JsonException ex)
            {
                lastException = ex;
                _logger.LogDebug("Spec generation attempt {Attempt} returned invalid JSON", attempt + 1);
            }
        }

        throw new LlmFormatException(lastRaw, lastException);
    }

    private string BuildPrompt(EnrichedTicket enriched, AgentConfig config, IReadOnlyList<CodeFileDto> codebaseContext)
    {
        var codebaseContextText = codebaseContext.Count > 0
            ? string.Join("\n\n", codebaseContext.Select(f => $"File: {f.Path}\n---\n{f.Content}"))
            : "No codebase context available.";

        var devRulesBlock = string.IsNullOrEmpty(config.DevRulesContent)
            ? string.Empty
            : $"## Development Rules\n\n{config.DevRulesContent}\n\n";

        return _promptTemplate
            .Replace("{{projectName}}", config.Project.Name)
            .Replace("{{language}}", config.Project.Language)
            .Replace("{{framework}}", config.Project.Framework)
            .Replace("{{architecture}}", config.Project.Architecture)
            .Replace("{{testFramework}}", config.Project.TestFramework)
            .Replace("{{naming}}", config.Conventions.Naming)
            .Replace("{{specStyle}}", config.Conventions.SpecStyle)
            .Replace("{{codebaseContext}}", codebaseContextText)
            .Replace("{{devRules}}", devRulesBlock)
            .Replace("{{workItemId}}", enriched.WorkItemId.ToString())
            .Replace("{{title}}", enriched.Title)
            .Replace("{{missingAcceptanceCriteria}}", string.Join(", ", enriched.MissingAcceptanceCriteria))
            .Replace("{{edgeCases}}", string.Join(", ", enriched.EdgeCases))
            .Replace("{{constraints}}", string.Join(", ", enriched.Constraints))
            .Replace("{{affectedComponents}}", string.Join(", ", enriched.AffectedComponents))
            .Replace("{{ambiguities}}", string.Join(", ", enriched.Ambiguities));
    }

    private static string ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : raw;
    }

    private static string LoadPrompt()
    {
        var assembly = typeof(SpecGeneratorAgent).Assembly;
        var dir = Path.GetDirectoryName(assembly.Location) ?? Directory.GetCurrentDirectory();
        var path = Path.Combine(dir, "Prompts", "spec.txt");
        return File.ReadAllText(path);
    }
}
