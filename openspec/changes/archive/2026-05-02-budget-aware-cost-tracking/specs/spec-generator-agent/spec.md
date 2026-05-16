## ADDED Requirements

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
