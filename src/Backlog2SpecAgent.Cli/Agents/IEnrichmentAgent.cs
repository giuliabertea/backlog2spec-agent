using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Config;
using Backlog2SpecAgent.Cli.Models;

namespace Backlog2SpecAgent.Cli.Agents;

public interface IEnrichmentAgent
{
    Task<EnrichedTicket> EnrichAsync(
        WorkItemDto workItem,
        AgentConfig config,
        IReadOnlyList<CodeFileDto> codebaseContext,
        CancellationToken ct = default);
}
