## ADDED Requirements

### Requirement: Return static CodeFileDto list without external calls
`MockCodebaseContextAgent` SHALL implement `ICodebaseContextAgent` and return a hardcoded `IReadOnlyList<CodeFileDto>` containing at least one entry with a plausible file path, file name, and a short C# snippet. It SHALL never make any HTTP call or read any file from disk.

#### Scenario: Returns non-empty list for any input
- **WHEN** `FetchRelevantFilesAsync` is called with any `WorkItemDto` and any `AgentConfig`
- **THEN** a non-empty list of `CodeFileDto` is returned synchronously (via `Task.FromResult`)

#### Scenario: No HTTP calls made in mock mode
- **WHEN** `MockCodebaseContextAgent` is registered and the pipeline runs
- **THEN** no outbound HTTP request to any ADO endpoint is made for codebase retrieval

### Requirement: Usable as drop-in replacement for CodebaseContextAgent in --mock mode
`MockCodebaseContextAgent` SHALL be registered in the DI container when `--mock` is passed, replacing `CodebaseContextAgent`. The pipeline SHALL complete successfully end-to-end using the mock's static data.

#### Scenario: Mock pipeline completes without secrets configured
- **WHEN** the CLI is invoked with `--mock` and no user-secrets are set
- **THEN** the pipeline completes and outputs a spec without any authentication errors
