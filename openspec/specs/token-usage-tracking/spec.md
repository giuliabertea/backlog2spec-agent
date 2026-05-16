# token-usage-tracking

## Purpose

TBD — tracks cumulative token usage across LLM calls and enforces a configurable USD budget limit to prevent runaway costs.

## Requirements

### Requirement: Accumulate token counts across LLM calls
The `TokenUsageTracker` service SHALL maintain a running total of input and output tokens via `void AddUsage(int inputTokens, int outputTokens)`. Both counters SHALL be thread-safe and initialized to zero at construction. Negative values SHALL be treated as zero.

#### Scenario: Sequential calls accumulate correctly
- **WHEN** `AddUsage(100, 50)` is called twice
- **THEN** the tracker holds 200 input tokens and 100 output tokens

#### Scenario: Concurrent calls do not lose updates
- **WHEN** `AddUsage` is called from multiple threads simultaneously
- **THEN** the final totals equal the sum of all individual inputs, with no lost updates

#### Scenario: Zero-token call is accepted without error
- **WHEN** `AddUsage(0, 0)` is called
- **THEN** the call completes without throwing and totals are unchanged

### Requirement: Estimate USD cost from accumulated tokens
`TokenUsageTracker.GetEstimatedCost()` SHALL return a `decimal` representing the estimated USD cost of all accumulated tokens. The calculation SHALL use fixed pricing constants: input tokens at $2.50 per 1,000,000 tokens and output tokens at $10.00 per 1,000,000 tokens. No external service or configuration SHALL be consulted for pricing.

#### Scenario: Cost reflects correct pricing constants
- **WHEN** `AddUsage(1_000_000, 0)` has been called
- **THEN** `GetEstimatedCost()` returns `2.50m`

#### Scenario: Output tokens priced at 4x input rate
- **WHEN** `AddUsage(0, 1_000_000)` has been called
- **THEN** `GetEstimatedCost()` returns `10.00m`

#### Scenario: Zero usage returns zero cost
- **WHEN** no calls to `AddUsage` have been made
- **THEN** `GetEstimatedCost()` returns `0m`

### Requirement: Enforce budget limit with hard stop
`TokenUsageTracker.EnforceBudget()` SHALL compare `GetEstimatedCost()` against the configured `BudgetLimit`. If the estimated cost equals or exceeds the limit, it SHALL throw `BudgetExceededException` carrying the current cost and the limit. If the cost is below the limit, it SHALL return without side effects. `BudgetLimit` SHALL default to `20m` (USD) and be settable by callers before the pipeline begins.

#### Scenario: EnforceBudget throws when cost meets or exceeds limit
- **WHEN** `GetEstimatedCost()` returns a value greater than or equal to `BudgetLimit`
- **THEN** `EnforceBudget()` throws `BudgetExceededException` with `CurrentCost` and `BudgetLimit` populated

#### Scenario: EnforceBudget succeeds when cost is below limit
- **WHEN** `GetEstimatedCost()` returns a value strictly less than `BudgetLimit`
- **THEN** `EnforceBudget()` returns without throwing

#### Scenario: Default budget limit is $20
- **WHEN** a new `TokenUsageTracker` is constructed without any configuration
- **THEN** `BudgetLimit` equals `20m`

#### Scenario: Budget limit can be overridden before pipeline starts
- **WHEN** `BudgetLimit` is set to `50m` before any LLM calls
- **THEN** `EnforceBudget()` does not throw until `GetEstimatedCost()` reaches or exceeds `50m`

### Requirement: BudgetExceededException carries cost context
`BudgetExceededException` SHALL be a domain exception that includes `decimal CurrentCost`, `decimal BudgetLimit`, and a human-readable `Message` suitable for CLI display. It SHALL not wrap another exception and SHALL not require external dependencies.

#### Scenario: Exception message includes cost and limit values
- **WHEN** `BudgetExceededException` is constructed with `currentCost = 20.37m` and `budgetLimit = 20.00m`
- **THEN** `Message` contains both values and is readable without a stack trace

#### Scenario: Exception properties are accessible after throw and catch
- **WHEN** `BudgetExceededException` is thrown and caught
- **THEN** `ex.CurrentCost` and `ex.BudgetLimit` return the values provided at construction
