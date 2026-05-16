# ado-client

## Purpose

The `AdoClient` is responsible for fetching work items from Azure DevOps using PAT-based authentication. It translates raw ADO API responses into typed `WorkItemDto` objects, handles HTML stripping from rich-text fields, and surfaces typed exceptions for authentication and not-found errors.

## Requirements

### Requirement: Fetch work item by ID from Azure DevOps
The `AdoClient` SHALL fetch a single work item by integer ID from Azure DevOps using `WorkItemHttpClient` with `VssBasicCredential` (PAT-based auth). The organization URL and project name SHALL come exclusively from `AgentConfig` (loaded from `backlog-2-spec.json`). The PAT SHALL come exclusively from user-secrets (`Ado:Pat`). No credentials SHALL appear in source code, config files, or logs.

#### Scenario: Valid work item ID returns populated WorkItemDto
- **WHEN** `GetWorkItemAsync(id)` is called with a valid work item ID
- **THEN** the system returns a `WorkItemDto` with `Id`, `Title`, `WorkItemType`, `Description`, and `AcceptanceCriteria` populated from ADO fields

#### Scenario: Missing optional fields default to empty string
- **WHEN** a work item exists but `Description` or `AcceptanceCriteria` fields are null or absent
- **THEN** those properties on `WorkItemDto` SHALL be set to empty string, not null

#### Scenario: Invalid work item ID throws typed exception
- **WHEN** `GetWorkItemAsync(id)` is called with an ID that does not exist in the ADO project
- **THEN** the system throws an `AdoNotFoundException` with the ID included in the message

#### Scenario: Authentication failure throws typed exception
- **WHEN** the PAT is invalid or expired
- **THEN** the system throws an `AdoAuthException` with a human-readable message (no PAT value in message)

### Requirement: Strip HTML from ADO field values
The `AdoClient` SHALL strip HTML markup from `Description` and `AcceptanceCriteria` fields before populating `WorkItemDto`. HTML stripping SHALL use `HtmlAgilityPack`; regex-based stripping is prohibited.

#### Scenario: HTML tags removed, text content preserved
- **WHEN** a field value contains HTML such as `<p>Some <b>text</b></p>`
- **THEN** the resulting string contains `Some text` with no HTML tags

#### Scenario: Malformed HTML handled without exception
- **WHEN** a field value contains malformed or unclosed HTML tags
- **THEN** the system strips what it can and returns plain text without throwing

### Requirement: Fetch with full field expansion
The `AdoClient` SHALL call `GetWorkItemAsync(id, expand: WorkItemExpand.All)` to ensure custom fields (e.g., `Microsoft.VSTS.Common.AcceptanceCriteria`) are included in the response.

#### Scenario: AcceptanceCriteria field is populated when present
- **WHEN** a work item has the `Microsoft.VSTS.Common.AcceptanceCriteria` field set
- **THEN** `WorkItemDto.AcceptanceCriteria` contains the stripped text value of that field
