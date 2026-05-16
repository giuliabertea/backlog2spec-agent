# spec-generator-agent

## Purpose

The `SpecGeneratorAgent` is responsible for transforming an `EnrichedTicket` into a structured `GeneratedSpec` using an Azure OpenAI model via Semantic Kernel. It enforces a fixed section order (Goal, Behaviour, EdgeCases, OutOfScope, FilesToChange), applies retry logic for LLM format failures, and uses a low temperature setting to maximize output determinism.

## Requirements

### Requirement: Generate structured spec from enriched ticket
The `SpecGeneratorAgent` SHALL accept an `EnrichedTicket` and `AgentConfig`, invoke the Azure OpenAI model via Semantic Kernel, and return a `GeneratedSpec`. The `GeneratedSpec` SHALL always contain exactly these sections in this order: `Goal`, `Behaviour`, `EdgeCases`, `OutOfScope`, `FilesToChange`. No additional sections SHALL be added.

#### Scenario: Valid enriched ticket produces fully populated GeneratedSpec
- **WHEN** `GenerateAsync(enrichedTicket, config)` is called with a complete `EnrichedTicket`
- **THEN** the returned `GeneratedSpec` has non-empty `Goal`, `Behaviour`, `EdgeCases`, `OutOfScope`, and `FilesToChange` properties

#### Scenario: Behaviour formatted as plain-English implementation bullets
- **WHEN** the `GeneratedSpec` is returned
- **THEN** each item in `Behaviour` is a plain-English sentence describing an implementation behaviour, and no item starts with `Given`, `When`, `Then`, or `Scenario:`

#### Scenario: Section ordering is always stable
- **WHEN** `GenerateAsync` is called multiple times with the same input
- **THEN** the `GeneratedSpec` always has the same section order: Goal, Behaviour, EdgeCases, OutOfScope, FilesToChange

### Requirement: LLM response must be valid JSON matching GeneratedSpec schema
The `SpecGeneratorAgent` SHALL validate that the LLM response parses to a valid `GeneratedSpec`. If parsing fails, the agent SHALL retry the prompt up to 2 additional times. If all attempts fail, the agent SHALL throw an `LlmFormatException`.

#### Scenario: Invalid JSON triggers retry up to 2 times
- **WHEN** the LLM returns invalid JSON on the first attempt but valid JSON on the second
- **THEN** the agent returns the deserialized `GeneratedSpec` from the second attempt

#### Scenario: All 3 attempts fail with invalid JSON
- **WHEN** all 3 LLM responses are unparseable
- **THEN** the agent throws `LlmFormatException` with the last raw response attached

### Requirement: Deterministic output for same input
The `SpecGeneratorAgent` SHALL configure the LLM with temperature 0.1 via `KernelFactory` to minimize variance across runs on the same input.

#### Scenario: Repeated calls with same input produce structurally identical output
- **WHEN** `GenerateAsync` is called twice with identical `EnrichedTicket` and `AgentConfig`
- **THEN** both results have the same section structure and Gherkin scenario count

### Requirement: Check budget before each LLM call
The `SpecGeneratorAgent` SHALL call `TokenUsageTracker.EnforceBudget()` before invoking the Azure OpenAI model. If `EnforceBudget()` throws `BudgetExceededException`, the agent SHALL allow the exception to propagate immediately without retrying and without logging the full prompt.

#### Scenario: BudgetExceededException propagates before any LLM call is made
- **WHEN** `TokenUsageTracker.EnforceBudget()` throws `BudgetExceededException` at the start of a call
- **THEN** `GenerateAsync` propagates the exception and no request is sent to Azure OpenAI

#### Scenario: Budget check fires before retry attempts
- **WHEN** `EnforceBudget()` throws on the second attempt of a retry loop
- **THEN** no further LLM calls are made and `BudgetExceededException` propagates out of `GenerateAsync`

### Requirement: Report token usage after each successful LLM response
After receiving a successful `ChatMessageContent` response, the `SpecGeneratorAgent` SHALL extract `InputTokenCount` and `OutputTokenCount` from `response.Metadata["Usage"]` and pass them to `TokenUsageTracker.AddUsage()`. If the `"Usage"` key is absent or the cast fails, the agent SHALL call `AddUsage(0, 0)` and continue without throwing.

#### Scenario: Token counts extracted from metadata and reported
- **WHEN** the LLM returns a `ChatMessageContent` with valid usage metadata
- **THEN** `AddUsage` is called with the correct input and output token counts from that response

#### Scenario: Missing usage metadata does not fail spec generation
- **WHEN** `response.Metadata` does not contain the `"Usage"` key (e.g., in mock mode)
- **THEN** `AddUsage(0, 0)` is called and `GenerateAsync` completes normally
