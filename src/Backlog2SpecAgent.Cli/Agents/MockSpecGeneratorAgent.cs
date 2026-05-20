using Backlog2SpecAgent.Cli.Models;

namespace Backlog2SpecAgent.Cli.Agents;

public sealed class MockSpecGeneratorAgent : ISpecGeneratorAgent
{
    public Task<GeneratedSpec> GenerateAsync(int workItemId, CancellationToken ct = default)
    {
        return Task.FromResult(new GeneratedSpec
        {
            Goal = "Add a mock feature to validate the pipeline end-to-end. The mock returns fixed data to allow testing without an LLM call.",
            Behaviour =
            [
                "Return a fixed spec when called with any work item ID",
                "Return an error result when input is flagged as invalid"
            ],
            EdgeCases = ["Null input", "Extremely large payload"],
            OutOfScope = "Authentication, Authorization",
            FilesToChange =
            [
                "src/Backlog2SpecAgent.Cli/Agents/MockSpecGeneratorAgent.cs: return mock GeneratedSpec",
                "src/Backlog2SpecAgent.Cli/Agents/ISpecGeneratorAgent.cs: interface contract"
            ]
        });
    }
}
