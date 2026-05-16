using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Config;

namespace Backlog2SpecAgent.Cli.Agents;

public interface ICodebaseContextAgent
{
    Task<IReadOnlyList<CodeFileDto>> FetchRelevantFilesAsync(
        WorkItemDto workItem, AgentConfig config, CancellationToken ct = default);
}
