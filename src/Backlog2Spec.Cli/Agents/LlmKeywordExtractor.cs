using System.Text.Json;
using Backlog2SpecAgent.Cli.Ado;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Backlog2SpecAgent.Cli.Agents;

public sealed class LlmKeywordExtractor : IKeywordExtractor
{
    private readonly Microsoft.SemanticKernel.Kernel _kernel;
    private readonly ILogger<LlmKeywordExtractor> _logger;
    private readonly StopwordKeywordExtractor _fallback = new();

    public LlmKeywordExtractor(Microsoft.SemanticKernel.Kernel kernel, ILogger<LlmKeywordExtractor> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ExtractAsync(WorkItemDto workItem, CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildPrompt(workItem);
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var chat = new ChatHistory();
            chat.AddUserMessage(prompt);

            var response = await chatService.GetChatMessageContentAsync(
                chat,
                new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0 } },
                _kernel,
                ct);

            var raw = response.Content ?? string.Empty;
            var keywords = JsonSerializer.Deserialize<List<string>>(raw);
            if (keywords is { Count: > 0 })
                return keywords.AsReadOnly();

            _logger.LogWarning("LLM keyword extraction returned empty list, falling back to stopword extractor");
            return await _fallback.ExtractAsync(workItem, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM keyword extraction failed, falling back to stopword extractor");
            return await _fallback.ExtractAsync(workItem, ct);
        }
    }

    private static string BuildPrompt(WorkItemDto workItem)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Given this work item, list 5-8 technical terms and class-name fragments that are likely to appear in related source files.");
        sb.AppendLine("Reply with a JSON array of strings only, no explanation.");
        sb.AppendLine();
        sb.Append("Title: ").AppendLine(workItem.Title);

        if (!string.IsNullOrWhiteSpace(workItem.Description))
            sb.Append("Description: ").AppendLine(workItem.Description);

        return sb.ToString();
    }
}
