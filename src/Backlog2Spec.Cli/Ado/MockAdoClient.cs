namespace Backlog2SpecAgent.Cli.Ado;

public sealed class MockAdoClient : IAdoClient
{
    public Task<WorkItemDto> GetWorkItemAsync(int id, CancellationToken ct = default)
    {
        return Task.FromResult(new WorkItemDto
        {
            Id = id,
            Title = "Mock Work Item",
            Description = "This is a mock description",
            AcceptanceCriteria = "Sample acceptance criteria",
            WorkItemType = "User Story"
        });
    }

    public Task<WorkItemHierarchyDto> GetWorkItemHierarchyAsync(int parentId, CancellationToken ct = default)
    {
        var parent = new WorkItemDto
        {
            Id = parentId,
            Title = "Mock Feature",
            WorkItemType = "Feature",
            Description = "This is a mock feature description.",
            AcceptanceCriteria = string.Empty
        };

        var children = new List<WorkItemDto>
        {
            new()
            {
                Id = parentId + 1,
                Title = "Mock Child PBI One",
                WorkItemType = "Product Backlog Item",
                Description = "First mock child work item description.",
                AcceptanceCriteria = "Given the user is logged in, when they submit the form, then data is saved."
            },
            new()
            {
                Id = parentId + 2,
                Title = "Mock Child PBI Two",
                WorkItemType = "Product Backlog Item",
                Description = "Second mock child work item description.",
                AcceptanceCriteria = "Given valid input, when processed, then results are returned."
            }
        };

        return Task.FromResult(new WorkItemHierarchyDto(parent, children));
    }
}
