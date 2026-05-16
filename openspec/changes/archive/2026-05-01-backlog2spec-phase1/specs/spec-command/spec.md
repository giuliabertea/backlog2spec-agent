## ADDED Requirements

### Requirement: Implement backlog-2-spec spec <id> command
The CLI SHALL expose the command `backlog-2-spec spec <id>` where `<id>` is a required integer argument identifying the Azure DevOps work item. The command SHALL orchestrate the full pipeline: fetch → enrich → generate → render. All orchestration logic SHALL be in `SpecCommand`; no business logic SHALL appear in `Program.cs`.

#### Scenario: Valid ID runs full pipeline and renders spec
- **WHEN** `backlog-2-spec spec 12345` is called with a reachable ADO work item
- **THEN** the command fetches the work item, enriches it, generates the spec, and renders it to the terminal via `OutputRenderer`

#### Scenario: Non-integer ID argument rejected at parse time
- **WHEN** `backlog-2-spec spec abc` is called
- **THEN** `System.CommandLine` rejects the argument before the handler runs and prints usage help

### Requirement: Support --verbose flag
The `spec` command SHALL accept an optional `--verbose` flag. When enabled, the command SHALL pass verbose mode to `OutputRenderer`, which displays additional detail (e.g., raw `EnrichedTicket` fields before spec generation).

#### Scenario: --verbose flag shows extra output
- **WHEN** `backlog-2-spec spec 12345 --verbose` is called
- **THEN** the output includes enrichment detail not shown in the default render

#### Scenario: Default run without --verbose shows concise output
- **WHEN** `backlog-2-spec spec 12345` is called without `--verbose`
- **THEN** only the final rendered spec is shown, not intermediate enrichment data

### Requirement: Support --raw flag for JSON-only output
The `spec` command SHALL accept an optional `--raw` flag. When enabled, the command SHALL output the `GeneratedSpec` serialized as pretty-printed JSON to stdout and suppress all other console output (progress steps, colors, headers).

#### Scenario: --raw outputs valid JSON only
- **WHEN** `backlog-2-spec spec 12345 --raw` is called
- **THEN** stdout contains only valid, pretty-printed JSON representing the `GeneratedSpec`; no progress text, colors, or headers are emitted

#### Scenario: --raw output can be piped to jq
- **WHEN** `backlog-2-spec spec 12345 --raw | jq .summary` is called
- **THEN** the pipeline succeeds and returns the summary string value

### Requirement: Errors are caught and displayed cleanly, never crash
The `SpecCommand` SHALL wrap the entire pipeline in a try/catch. Typed exceptions (`AdoNotFoundException`, `AdoAuthException`, `LlmFormatException`, `ConfigException`) SHALL display a human-readable error message via `OutputRenderer`. Unhandled exceptions SHALL display a generic error with the exception message. The process SHALL exit with a non-zero exit code on error.

#### Scenario: ADO work item not found shows friendly error
- **WHEN** the specified work item ID does not exist
- **THEN** the command displays "Work item {id} not found." and exits with code 1

#### Scenario: Unhandled exception shows generic error without stack trace
- **WHEN** an unexpected exception occurs during pipeline execution
- **THEN** the command displays "Unexpected error: {message}" and exits with code 1 without printing a stack trace

#### Scenario: Process exit code is 0 on success
- **WHEN** the pipeline completes without error
- **THEN** the process exits with code 0
