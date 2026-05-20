using Backlog2SpecAgent.Cli.Models;

namespace Backlog2SpecAgent.Cli.Agents;

public interface ISpecGeneratorAgent
{
    Task<GeneratedSpec> GenerateAsync(int workItemId, CancellationToken ct = default);
}
