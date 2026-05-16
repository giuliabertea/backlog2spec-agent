## Context

Backlog2SpecAgent uses Azure OpenAI via Semantic Kernel. Both `EnrichmentAgent` and `SpecGeneratorAgent` call `IChatCompletionService.GetChatMessageContentAsync()`, which returns a `ChatMessageContent` object. Currently only `response.Content` is consumed; token usage metadata available in `response.Metadata` is discarded. There is no `Services/` directory yet. All registrations are flat singletons in `Program.cs`.

## Goals / Non-Goals

**Goals:**
- Track cumulative input/output token counts across all LLM calls in a process run
- Estimate USD cost from those counts using hardcoded pricing constants
- Throw `BudgetExceededException` before any LLM call that would push cost over the configured limit
- Allow `SpecCommand` to set a per-run budget limit via `--budget` flag
- Surface budget errors as a clean Spectre.Console panel, not a raw exception

**Non-Goals:**
- Persistent cross-run tracking (no database, no file writes)
- Per-model dynamic pricing (hardcoded constants for Phase 1)
- Token estimation without SK metadata (if SK provides no usage data, those tokens are silently untracked)
- Modifying ADO logic, `OutputRenderer`, or mock agents

## Decisions

### 1. Budget limit lives on `TokenUsageTracker`, not passed per-call

**Decision:** `TokenUsageTracker` exposes a `decimal BudgetLimit { get; set; }` (default `20m`). `SpecCommand` sets it once before the pipeline runs. Agents call `_tokenTracker.EnforceBudget()` — a convenience method that checks the stored limit and throws.

**Alternatives considered:**
- *Pass limit as parameter to agent methods*: Would require changing `IEnrichmentAgent` and `ISpecGeneratorAgent` signatures — unnecessary coupling between budget policy and agent contracts.
- *Inject a `BudgetOptions` singleton*: Cleaner in theory, but adds an extra type for a single decimal value. Over-engineered for Phase 1.

**Why this:** Zero interface churn. `SpecCommand` already owns orchestration; setting a property on a singleton it controls is consistent with how `ConfigLoader` is used today.

### 2. Extract token usage from `response.Metadata["Usage"]`

**Decision:** After each `GetChatMessageContentAsync()` call, access `response.Metadata?["Usage"]` and cast to `Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIUsage`. Extract `InputTokenCount` and `OutputTokenCount`. If the cast fails or the key is absent (e.g., mock mode), call `AddUsage(0, 0)` — tracking silently no-ops.

**Alternatives considered:**
- *Cast `response.InnerContent` to `Azure.AI.OpenAI.ChatCompletion`*: More direct but creates a hard dependency on the Azure OpenAI SDK type, breaking mock compatibility.
- *Manual token estimation (tiktoken-style)*: Explicitly forbidden by the requirements.

**Why this:** `Metadata` is the SK-idiomatic, provider-neutral surface for usage data. Null-safe access means mock agents (which return no metadata) continue working without modification.

### 3. Thread safety via `Interlocked` on `long` fields

**Decision:** Store `_inputTokens` and `_outputTokens` as `long` fields. Use `Interlocked.Add()` in `AddUsage()`. No `lock` block needed for the accumulation path.

**Alternatives considered:**
- *`lock` on a private object*: Correct but heavier than needed for two independent counters.
- *`decimal` directly with `lock`*: `Interlocked` doesn't support `decimal`; cost is computed on-demand from integer counters, so `decimal` arithmetic only happens in `GetEstimatedCost()` which is called rarely and read-only.

**Why this:** `Interlocked.Add` is sufficient, non-blocking, and idiomatic for simple counters.

### 4. `EnforceBudget()` throws before the LLM call, not after

**Decision:** Agents call `EnforceBudget()` as the first line inside each retry-guarded block, before constructing the chat history or calling SK.

**Why this:** Ensures no LLM call is ever initiated once the budget is exceeded, even if a retry loop would attempt it again. Placing the check after the call would still consume tokens.

### 5. Pricing constants are sealed in `TokenUsageTracker`

**Decision:** `InputCostPerMillionTokens = 2.50m` and `OutputCostPerMillionTokens = 10.00m` are `private const decimal` fields inside `TokenUsageTracker`.

**Why this:** Phase 1 requirement. Keeping them private prevents callers from accidentally depending on the pricing model and makes the eventual externalization (config file, enum) a contained change.

## Risks / Trade-offs

- **Metadata key may change across SK versions** → Mitigation: null-safe cast with fallback to zero; a unit test against a real SK response object will catch a key rename at upgrade time.
- **Singleton tracker accumulates across retries** → If an agent retries after `JsonException`, the first failed call's tokens are counted. Accepted: cost was incurred regardless of whether the JSON parse succeeded.
- **No cross-run accumulation** → A long pipeline split across multiple CLI invocations could silently exceed monthly budget. Accepted for Phase 1; persistent tracking is a future capability.
- **Hardcoded pricing becomes stale** → Model pricing changes over time. Accepted for Phase 1; the constants are in one place and easy to update.

## Migration Plan

1. Add `Exceptions/BudgetExceededException.cs`
2. Add `Services/TokenUsageTracker.cs`
3. Register `TokenUsageTracker` as singleton in `Program.cs` (both mock and non-mock paths)
4. Update `EnrichmentAgent` — inject tracker, add `EnforceBudget()` pre-call, add `AddUsage()` post-call
5. Update `SpecGeneratorAgent` — same as above
6. Update `SpecCommand` — inject tracker, set `BudgetLimit` from `--budget` option, catch `BudgetExceededException`

No rollback strategy needed — all changes are additive or confined to existing files. Reverting is a straight `git revert`.

## Open Questions

- Should `TokenUsageTracker` also log a warning (not throw) when cost crosses 80% of budget? Left for Phase 2.
- Azure OpenAI returns a `prompt_tokens` count that includes system prompt tokens. Is that acceptable, or should we track only user-content tokens? Accepted as-is — system prompt cost is real cost.
