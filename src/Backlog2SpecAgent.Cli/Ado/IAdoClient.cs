namespace Backlog2SpecAgent.Cli.Ado;

public interface IAdoClient
{
    Task<WorkItemDto> GetWorkItemAsync(int id, CancellationToken ct = default);
    Task<WorkItemHierarchyDto> GetWorkItemHierarchyAsync(int parentId, CancellationToken ct = default);
}
