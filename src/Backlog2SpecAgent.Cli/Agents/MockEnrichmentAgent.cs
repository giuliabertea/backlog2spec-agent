using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Config;
using Backlog2SpecAgent.Cli.Models;

namespace Backlog2SpecAgent.Cli.Agents;

public sealed class MockEnrichmentAgent : IEnrichmentAgent
{
    public Task<EnrichedTicket> EnrichAsync(
        WorkItemDto workItem,
        AgentConfig config,
        IReadOnlyList<CodeFileDto> codebaseContext,
        CancellationToken ct = default)
    {
        return Task.FromResult(new EnrichedTicket
        {
            WorkItemId = workItem.Id,
            Title = workItem.Title,
            MissingAcceptanceCriteria = ["Validation rules not defined"],
            EdgeCases = ["Null input", "Large payload", "Timeout handling"],
            Constraints = ["Must support .NET 8", "REST API only"],
            AffectedComponents = ["API", "Application Layer"],
            Ambiguities = ["Expected max payload size unknown"]
        });
    }
}
