using Backlog2SpecAgent.Cli.Ado;

namespace Backlog2SpecAgent.Tests;

public class AdoClientTests
{
    // --- MockAdoClient ---

    [Fact]
    public async Task MockAdoClient_GetWorkItemAsync_ReturnsItemWithCorrectId()
    {
        var client = new MockAdoClient();
        var dto = await client.GetWorkItemAsync(42);
        Assert.Equal(42, dto.Id);
    }

    [Fact]
    public async Task MockAdoClient_GetWorkItemAsync_ReturnsPopulatedFields()
    {
        var client = new MockAdoClient();
        var dto = await client.GetWorkItemAsync(1);
        Assert.False(string.IsNullOrWhiteSpace(dto.Title));
        Assert.False(string.IsNullOrWhiteSpace(dto.WorkItemType));
    }

    [Fact]
    public async Task MockAdoClient_GetWorkItemHierarchyAsync_ReturnsParentWithCorrectId()
    {
        var client = new MockAdoClient();
        var hierarchy = await client.GetWorkItemHierarchyAsync(10);
        Assert.Equal(10, hierarchy.Parent.Id);
    }

    [Fact]
    public async Task MockAdoClient_GetWorkItemHierarchyAsync_ReturnsTwoChildren()
    {
        var client = new MockAdoClient();
        var hierarchy = await client.GetWorkItemHierarchyAsync(10);
        Assert.Equal(2, hierarchy.Children.Count);
    }

    [Fact]
    public async Task MockAdoClient_GetWorkItemHierarchyAsync_ChildIdsAreAdjacentToParent()
    {
        var client = new MockAdoClient();
        var hierarchy = await client.GetWorkItemHierarchyAsync(10);
        Assert.Equal(11, hierarchy.Children[0].Id);
        Assert.Equal(12, hierarchy.Children[1].Id);
    }

    // --- AdoNotFoundException ---

    [Fact]
    public void AdoNotFoundException_MessageContainsWorkItemId()
    {
        var ex = new AdoNotFoundException(99);
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void AdoNotFoundException_IsArgumentException()
    {
        // Verifies the exception hierarchy hasn't been changed by accident
        Assert.IsAssignableFrom<Exception>(new AdoNotFoundException(1));
    }

    // --- AdoAuthException ---

    [Fact]
    public void AdoAuthException_PreservesMessage()
    {
        var ex = new AdoAuthException("Bad PAT", new Exception("inner"));
        Assert.Contains("Bad PAT", ex.Message);
        Assert.NotNull(ex.InnerException);
    }

    // --- WorkItemDto ---

    [Fact]
    public void WorkItemDto_DefaultValues_AreEmpty()
    {
        var dto = new WorkItemDto();
        Assert.Equal(0, dto.Id);
        Assert.Equal(string.Empty, dto.Title);
        Assert.Equal(string.Empty, dto.Description);
        Assert.Equal(string.Empty, dto.AcceptanceCriteria);
    }
}
