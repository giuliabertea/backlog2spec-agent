# spec-command

## Purpose

`SpecCommand` is the top-level CLI command handler for `backlog-2-spec spec <id>`. It orchestrates the full pipeline (fetch → enrich → generate → render), owns all error handling and typed exception mapping, and exposes `--verbose` and `--raw` flags. No business logic lives in `Program.cs`.

## Requirements

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

### Requirement: Support --budget option to configure monthly spend limit
The `spec` command SHALL accept an optional `--budget <amount>` option where `<amount>` is a positive decimal value in USD. When provided, `SpecCommand` SHALL set `TokenUsageTracker.BudgetLimit` to this value before running the pipeline. When omitted, the tracker's default limit of `$20.00` SHALL apply.

#### Scenario: --budget overrides the default tracker limit
- **WHEN** `backlog-2-spec spec 12345 --budget 50` is called
- **THEN** `TokenUsageTracker.BudgetLimit` is set to `50m` before any agent is invoked

#### Scenario: Omitting --budget uses the $20 default
- **WHEN** `backlog-2-spec spec 12345` is called without `--budget`
- **THEN** `TokenUsageTracker.BudgetLimit` remains at its default value of `20m`

#### Scenario: Non-positive --budget value is rejected at parse time
- **WHEN** `backlog-2-spec spec 12345 --budget 0` or `--budget -5` is called
- **THEN** the argument parser rejects the value and prints usage help before the handler runs

### Requirement: Errors are caught and displayed cleanly, never crash
The `SpecCommand` SHALL wrap the entire pipeline in a try/catch. Typed exceptions (`AdoNotFoundException`, `AdoAuthException`, `LlmFormatException`, `ConfigException`, `BudgetExceededException`) SHALL display a human-readable error message via a Spectre.Console panel. `BudgetExceededException` SHALL render a panel with title "Budget Exceeded" containing the current cost, the configured limit, and a tip to reduce prompt size or increase the budget. Unhandled exceptions SHALL display a generic error with the exception message. The process SHALL exit with a non-zero exit code on error.

#### Scenario: ADO work item not found shows friendly error
- **WHEN** the specified work item ID does not exist
- **THEN** the command displays "Work item {id} not found." and exits with code 1

#### Scenario: Unhandled exception shows generic error without stack trace
- **WHEN** an unexpected exception occurs during pipeline execution
- **THEN** the command displays "Unexpected error: {message}" and exits with code 1 without printing a stack trace

#### Scenario: Process exit code is 0 on success
- **WHEN** the pipeline completes without error
- **THEN** the process exits with code 0

#### Scenario: BudgetExceededException renders a formatted error panel
- **WHEN** `BudgetExceededException` is thrown during pipeline execution
- **THEN** a Spectre.Console panel with title "Budget Exceeded" is displayed showing the current cost and configured limit, followed by a suggestion to reduce prompt size or adjust the budget, and the process exits with code 1

#### Scenario: BudgetExceededException panel shows accurate cost values
- **WHEN** the budget is exceeded at `$20.37` against a `$20.00` limit
- **THEN** the panel body shows "Current cost: $20.37" and "Limit: $20.00"

### Requirement: Support --mock flag for pipeline execution without external calls
The `spec` command SHALL accept an optional `--mock` flag. When enabled, the command SHALL use mock implementations (`MockAdoClient`, `MockEnrichmentAgent`, `MockSpecGeneratorAgent`) instead of real ones. All external calls (Azure DevOps, Azure OpenAI) SHALL be skipped. Output SHALL still route through `OutputRenderer` with the same formatting as real mode. The process SHALL log `[MOCK MODE ENABLED]` at Information level when this flag is active.

#### Scenario: --mock runs full pipeline without credentials
- **WHEN** `backlog-2-spec spec 99 --mock` is called with no secrets configured
- **THEN** the command completes successfully, renders the mock spec to the terminal, and exits with code 0

#### Scenario: --mock output uses OutputRenderer with normal formatting
- **WHEN** `backlog-2-spec spec 99 --mock` is called
- **THEN** the rendered output uses the same headers, colors, and layout as a real run

#### Scenario: --mock combined with --verbose shows verbose detail
- **WHEN** `backlog-2-spec spec 99 --mock --verbose` is called
- **THEN** the enrichment detail section is rendered in addition to the generated spec

#### Scenario: --mock combined with --raw outputs JSON only
- **WHEN** `backlog-2-spec spec 99 --mock --raw` is called
- **THEN** stdout contains only valid JSON representing the mock `GeneratedSpec`

#### Scenario: Without --mock real implementations are used
- **WHEN** `backlog-2-spec spec <id>` is called without `--mock`
- **THEN** the real `AdoClient`, `EnrichmentAgent`, and `SpecGeneratorAgent` are used as before
