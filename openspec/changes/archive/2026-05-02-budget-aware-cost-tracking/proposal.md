## Why

Backlog2SpecAgent makes LLM calls without any cost guardrails — a single run can silently consume unbounded AI spend. Adding a hard-stop budget limit prevents accidental overage and gives operators a reliable monthly cost ceiling.

## What Changes

- Add `Services/TokenUsageTracker.cs` — singleton service that accumulates input/output token counts and estimates USD cost using hardcoded per-model pricing
- Add `Exceptions/BudgetExceededException.cs` — domain exception carrying current cost and budget limit for clean CLI surfacing
- Update `EnrichmentAgent` — extract token usage from Semantic Kernel responses and call `TokenUsageTracker.AddUsage()`; check budget before each LLM call
- Update `SpecGeneratorAgent` — same integration as EnrichmentAgent
- Update `SpecCommand` — catch `BudgetExceededException` and display a Spectre.Console error panel; add optional `--budget <amount>` CLI flag to override the $20 default
- Register `TokenUsageTracker` as a singleton in DI

## Capabilities

### New Capabilities

- `token-usage-tracking`: Centralized token accumulation, USD cost estimation, and hard-stop budget enforcement for all LLM calls

### Modified Capabilities

- `enrichment-agent`: Adds pre-call budget check (throws `BudgetExceededException` if limit reached) and post-call usage reporting to `TokenUsageTracker`
- `spec-generator-agent`: Same budget-check and usage-reporting behavior as `enrichment-agent`
- `spec-command`: Adds `--budget <decimal>` option; catches `BudgetExceededException` and renders a formatted error panel instead of propagating the exception

## Impact

- **New files**: `Services/TokenUsageTracker.cs`, `Exceptions/BudgetExceededException.cs`
- **Modified files**: `Commands/SpecCommand.cs`, agent files for `EnrichmentAgent` and `SpecGeneratorAgent`, DI registration (likely `Program.cs` or service registration class)
- **No ADO logic changes** — budget tracking is orthogonal to ADO API calls
- **No `OutputRenderer` changes** — error display goes through Spectre.Console directly in `SpecCommand`
- **No external dependencies** — cost estimation is fully in-process with hardcoded pricing constants
