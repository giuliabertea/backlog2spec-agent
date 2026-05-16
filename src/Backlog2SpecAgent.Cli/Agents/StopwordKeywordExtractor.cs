using Backlog2SpecAgent.Cli.Ado;

namespace Backlog2SpecAgent.Cli.Agents;

public sealed class StopwordKeywordExtractor : IKeywordExtractor
{
    private static readonly string[] Stopwords =
    [
        "the", "and", "for", "with", "this", "that", "from", "have",
        "not", "are", "was", "will", "add", "new", "fix", "update",
        "when", "then", "given", "user", "should", "must", "able",
        "into", "onto", "also", "each", "some", "more"
    ];

    public Task<IReadOnlyList<string>> ExtractAsync(WorkItemDto workItem, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workItem.Title))
            return Task.FromResult<IReadOnlyList<string>>([]);

        var text = $"{workItem.Title} {workItem.WorkItemType}";
        IReadOnlyList<string> keywords = [.. text
            .Split([' ', '-', '_', '/', '\\', '.', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length > 3 && !Stopwords.Contains(w))
            .Distinct()
            .Take(5)];

        return Task.FromResult(keywords);
    }
}
