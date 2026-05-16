namespace Backlog2SpecAgent.Cli.Ado;

public sealed record WorkItemHierarchyDto(
    WorkItemDto Parent,
    IReadOnlyList<WorkItemDto> Children);
