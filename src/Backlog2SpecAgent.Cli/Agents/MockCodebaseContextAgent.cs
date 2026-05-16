using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Config;

namespace Backlog2SpecAgent.Cli.Agents;

public sealed class MockCodebaseContextAgent : ICodebaseContextAgent
{
    public Task<IReadOnlyList<CodeFileDto>> FetchRelevantFilesAsync(
        WorkItemDto workItem, AgentConfig config, CancellationToken ct = default)
    {
        IReadOnlyList<CodeFileDto> files =
        [
            new CodeFileDto
            {
                Path = "/src/Application/Profitability/ProfitabilityService.cs",
                FileName = "ProfitabilityService.cs",
                Content = "public sealed class ProfitabilityService : IProfitabilityService\n{\n    private readonly ISnapshotRepository _snapshotRepo;\n    private readonly IBookingRepository _bookingRepo;\n\n    public ProfitabilityService(\n        ISnapshotRepository snapshotRepo,\n        IBookingRepository bookingRepo)\n    {\n        _snapshotRepo = snapshotRepo;\n        _bookingRepo = bookingRepo;\n    }\n\n    public async Task<ProfitabilityResult> CalculateAsync(int bookingId, CancellationToken ct)\n    {\n        // ...\n    }\n}"
            }
        ];
        return Task.FromResult(files);
    }
}
