## ADDED Requirements

### Requirement: Fetch parent work item with its direct children
`IAdoClient` SHALL expose `GetWorkItemHierarchyAsync(int parentId, CancellationToken ct)` that fetches the parent work item (Feature or Epic) with `WorkItemExpand.Relations`, resolves all `System.LinkTypes.Hierarchy-Forward` relations to obtain child IDs, batch-fetches those children with `WorkItemExpand.All`, and returns a `WorkItemHierarchyDto` containing the parent `WorkItemDto` and an ordered list of child `WorkItemDto` objects. If the parent has no children, the list SHALL be empty (not null).

#### Scenario: Feature with children returns populated hierarchy
- **WHEN** `GetWorkItemHierarchyAsync(id)` is called with a Feature ID that has 3 child PBIs
- **THEN** the returned `WorkItemHierarchyDto.Parent` is the Feature's `WorkItemDto` and `WorkItemHierarchyDto.Children` contains exactly 3 `WorkItemDto` entries with all standard fields populated

#### Scenario: Work item with no children returns empty children list
- **WHEN** `GetWorkItemHierarchyAsync(id)` is called with a work item that has no `Hierarchy-Forward` relations
- **THEN** the returned `WorkItemHierarchyDto.Children` is an empty list and no exception is thrown

#### Scenario: Invalid parent ID throws AdoNotFoundException
- **WHEN** `GetWorkItemHierarchyAsync(id)` is called with a non-existent work item ID
- **THEN** the system throws `AdoNotFoundException` with the ID in the message, same as `GetWorkItemAsync`

#### Scenario: Authentication failure throws AdoAuthException
- **WHEN** the PAT is invalid or expired and `GetWorkItemHierarchyAsync` is called
- **THEN** the system throws `AdoAuthException` with a human-readable message

### Requirement: WorkItemHierarchyDto model
The system SHALL define a `WorkItemHierarchyDto` record with `WorkItemDto Parent` and `IReadOnlyList<WorkItemDto> Children` properties. All HTML stripping and field-mapping rules that apply to `WorkItemDto` SHALL apply equally to the parent and each child.

#### Scenario: WorkItemHierarchyDto children preserve field integrity
- **WHEN** a child work item has HTML in its `Description` field
- **THEN** `WorkItemHierarchyDto.Children[i].Description` contains the stripped plain-text value, not raw HTML

### Requirement: MockAdoClient implements GetWorkItemHierarchyAsync
`MockAdoClient` SHALL implement `GetWorkItemHierarchyAsync` and return a hard-coded `WorkItemHierarchyDto` with a mock Feature parent and at least two mock child `WorkItemDto` entries. The mock SHALL not make any network calls.

#### Scenario: Mock hierarchy returns without credentials
- **WHEN** `MockAdoClient.GetWorkItemHierarchyAsync(anyId)` is called with no ADO credentials configured
- **THEN** it returns a populated `WorkItemHierarchyDto` without throwing and without making any network calls
