using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Config;

namespace Backlog2SpecAgent.Cli.Agents;

public interface ICodebaseContextAgent
{
    Task<IReadOnlyList<CodeFileDto>> FetchRelevantFilesAsync(
        WorkItemDto workItem, BacklogConfig config, CancellationToken ct = default);
}
