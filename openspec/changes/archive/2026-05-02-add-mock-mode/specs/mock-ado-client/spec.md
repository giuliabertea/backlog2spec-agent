## ADDED Requirements

### Requirement: Provide deterministic work item fetch without ADO calls
`MockAdoClient` SHALL implement `IAdoClient` and return a fixed, hardcoded `WorkItemDto` for any input ID. It SHALL make no external calls (no HTTP, no Azure DevOps). The returned data SHALL be identical on every invocation for the same ID.

#### Scenario: Returns fixed work item data
- **WHEN** `GetWorkItemAsync` is called with any integer ID
- **THEN** the returned `WorkItemDto` has `Id` set to the requested ID and all other fields set to the hardcoded values defined in the implementation

#### Scenario: Makes no external network calls
- **WHEN** `GetWorkItemAsync` is called with no network access available
- **THEN** the method completes successfully and returns the fixed `WorkItemDto`

#### Scenario: Title is the hardcoded mock string
- **WHEN** `GetWorkItemAsync` is called
- **THEN** `WorkItemDto.Title` equals `"Mock Work Item"`

#### Scenario: Description is the hardcoded mock string
- **WHEN** `GetWorkItemAsync` is called
- **THEN** `WorkItemDto.Description` equals `"This is a mock description"`

#### Scenario: AcceptanceCriteria is the hardcoded mock string
- **WHEN** `GetWorkItemAsync` is called
- **THEN** `WorkItemDto.AcceptanceCriteria` equals `"Sample acceptance criteria"`

#### Scenario: WorkItemType is the hardcoded mock string
- **WHEN** `GetWorkItemAsync` is called
- **THEN** `WorkItemDto.WorkItemType` equals `"User Story"`
