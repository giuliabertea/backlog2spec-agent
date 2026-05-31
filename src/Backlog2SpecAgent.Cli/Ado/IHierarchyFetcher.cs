namespace Backlog2SpecAgent.Cli.Ado;

public interface IHierarchyFetcher
{
    Task<WorkItemHierarchyDto> GetHierarchyAsync(int id, CancellationToken ct = default);
}
