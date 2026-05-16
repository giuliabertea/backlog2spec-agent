using Backlog2SpecAgent.Cli.Ado;

namespace Backlog2SpecAgent.Cli.Agents;

public interface IKeywordExtractor
{
    Task<IReadOnlyList<string>> ExtractAsync(WorkItemDto workItem, CancellationToken ct = default);
}
