using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Config;
using Backlog2SpecAgent.Cli.Models;

namespace Backlog2SpecAgent.Cli.Agents;

public interface ISpecGeneratorAgent
{
    Task<GeneratedSpec> GenerateAsync(
        EnrichedTicket enriched,
        AgentConfig config,
        IReadOnlyList<CodeFileDto> codebaseContext,
        CancellationToken ct = default);
}
