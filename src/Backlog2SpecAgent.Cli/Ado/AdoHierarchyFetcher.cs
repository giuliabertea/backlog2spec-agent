namespace Backlog2SpecAgent.Cli.Ado;

public sealed class AdoHierarchyFetcher : IHierarchyFetcher
{
    private readonly IAdoClient _adoClient;

    public AdoHierarchyFetcher(IAdoClient adoClient) => _adoClient = adoClient;

    public Task<WorkItemHierarchyDto> GetHierarchyAsync(int id, CancellationToken ct = default)
        => _adoClient.GetWorkItemHierarchyAsync(id, ct);
}
