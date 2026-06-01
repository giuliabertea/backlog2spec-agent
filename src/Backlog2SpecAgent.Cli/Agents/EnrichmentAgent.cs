using System.Text.Json;
using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Config;
using Backlog2SpecAgent.Cli.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Backlog2SpecAgent.Cli.Agents;

public sealed class EnrichmentAgent : IEnrichmentAgent
{
    private const int MaxRetries = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly Microsoft.SemanticKernel.Kernel _kernel;
    private readonly ILogger<EnrichmentAgent> _logger;
    private readonly string _promptTemplate;

    public EnrichmentAgent(Microsoft.SemanticKernel.Kernel kernel, ILogger<EnrichmentAgent> logger)
    {
        _kernel = kernel;
        _logger = logger;
        _promptTemplate = LoadPrompt();
    }

    public async Task<EnrichedTicket> EnrichAsync(
        WorkItemDto workItem,
        BacklogConfig config,
        IReadOnlyList<CodeFileDto> codebaseContext,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting enrichment for work item {WorkItemId}", workItem.Id);

        var prompt = BuildPrompt(workItem, config, codebaseContext);
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
            _logger.LogDebug("Enrichment response size: {Chars} chars (attempt {Attempt})", lastRaw.Length, attempt + 1);

            try
            {
                var json = ExtractJson(lastRaw);
                var result = JsonSerializer.Deserialize<EnrichedTicket>(json, JsonOptions);
                if (result is not null)
                {
                    _logger.LogInformation("Enrichment completed for work item {WorkItemId}", workItem.Id);
                    return result;
                }
            }
            catch (JsonException ex)
            {
                lastException = ex;
                _logger.LogDebug("Enrichment attempt {Attempt} returned invalid JSON", attempt + 1);
            }
        }

        throw new LlmFormatException(lastRaw, lastException);
    }

    private string BuildPrompt(WorkItemDto workItem, BacklogConfig config, IReadOnlyList<CodeFileDto> codebaseContext)
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
            .Replace("{{naming}}", config.Conventions.Naming)
            .Replace("{{diPattern}}", config.Conventions.DiPattern)
            .Replace("{{codebaseContext}}", codebaseContextText)
            .Replace("{{devRules}}", devRulesBlock)
            .Replace("{{workItemId}}", workItem.Id.ToString())
            .Replace("{{title}}", workItem.Title)
            .Replace("{{workItemType}}", workItem.WorkItemType)
            .Replace("{{description}}", workItem.Description)
            .Replace("{{acceptanceCriteria}}", workItem.AcceptanceCriteria);
    }

    private static string ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : raw;
    }

    private static string LoadPrompt()
    {
        var assembly = typeof(EnrichmentAgent).Assembly;
        var dir = Path.GetDirectoryName(assembly.Location) ?? Directory.GetCurrentDirectory();
        var path = Path.Combine(dir, "Prompts", "enrichment.txt");
        return File.ReadAllText(path);
    }
}
