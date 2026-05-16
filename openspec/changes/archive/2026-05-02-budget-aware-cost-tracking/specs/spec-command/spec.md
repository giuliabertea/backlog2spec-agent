## ADDED Requirements

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

## MODIFIED Requirements

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
