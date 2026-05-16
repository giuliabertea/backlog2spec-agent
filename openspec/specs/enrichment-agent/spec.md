# enrichment-agent

## Purpose

The `EnrichmentAgent` is responsible for analyzing a raw `WorkItemDto` using an Azure OpenAI model via Semantic Kernel and producing an `EnrichedTicket`. It performs prompt-driven extraction of missing acceptance criteria, edge cases, constraints, affected components, and ambiguities, with retry logic to handle LLM format failures.

## Requirements

### Requirement: Enrich work item via Azure OpenAI
The `EnrichmentAgent` SHALL accept a `WorkItemDto`, an `AgentConfig`, and an `IReadOnlyList<CodeFileDto> codebaseContext`, invoke the Azure OpenAI model via Semantic Kernel, and return a populated `EnrichedTicket`. The prompt SHALL include the full JSON schema for `EnrichedTicket` and one example input/output pair. The prompt SHALL instruct the model not to guess — if information is not derivable from the work item, the relevant list SHALL be empty.

#### Scenario: Well-defined work item produces all enrichment sections
- **WHEN** `EnrichAsync(workItem, config, codebaseContext)` is called with a work item that has title, description, and acceptance criteria
- **THEN** the returned `EnrichedTicket` has non-empty `MissingAcceptanceCriteria`, `EdgeCases`, `Constraints`, `AffectedComponents`, and `Ambiguities` lists where information is inferable

#### Scenario: Sparse work item produces partial enrichment without exception
- **WHEN** `EnrichAsync(workItem, config, codebaseContext)` is called with a work item that has only a title
- **THEN** the returned `EnrichedTicket` has empty lists for sections that cannot be inferred, and the call completes without throwing

### Requirement: Include codebase context in enrichment prompt
When `codebaseContext` is non-empty, `EnrichmentAgent` SHALL format each `CodeFileDto` as a labeled snippet (`File: {Path}\n---\n{Content}`) and inject them under the `## Codebase Context` section of the enrichment prompt. When `codebaseContext` is empty, the section SHALL contain the literal text `"No codebase context available."`. The prompt SHALL instruct the model to prefer component names found in the codebase snippets over generic guesses.

#### Scenario: Non-empty context injects file snippets into prompt
- **WHEN** `codebaseContext` contains one or more `CodeFileDto` entries
- **THEN** the built prompt contains a `## Codebase Context` section with each file's path and content

#### Scenario: Empty context inserts placeholder text
- **WHEN** `codebaseContext` is an empty list
- **THEN** the built prompt contains `"No codebase context available."` under `## Codebase Context`

### Requirement: LLM response must be valid JSON matching EnrichedTicket schema
The `EnrichmentAgent` SHALL validate that the LLM response parses to a valid `EnrichedTicket`. If parsing fails, the agent SHALL retry the prompt up to 2 additional times. If all attempts fail, the agent SHALL throw an `LlmFormatException` containing the last raw LLM response.

#### Scenario: First-attempt valid JSON succeeds immediately
- **WHEN** the LLM returns valid `EnrichedTicket` JSON on the first attempt
- **THEN** the agent returns the deserialized object without retrying

#### Scenario: Invalid JSON on first attempt triggers retry
- **WHEN** the LLM returns malformed JSON on the first attempt but valid JSON on the second
- **THEN** the agent returns the valid deserialized object from the second attempt

#### Scenario: All attempts return invalid JSON throws LlmFormatException
- **WHEN** all 3 attempts (initial + 2 retries) return non-parseable responses
- **THEN** the agent throws `LlmFormatException` with the last raw response included

### Requirement: No secrets or full prompts logged
The `EnrichmentAgent` SHALL use `ILogger` to log at Info level when enrichment starts and completes, and Debug level for response size in characters. Full prompt text and API keys SHALL never appear in logs.

#### Scenario: Info log emitted at start and completion
- **WHEN** `EnrichAsync` is called and completes successfully
- **THEN** exactly two Info-level log entries are emitted: one at start, one at completion

#### Scenario: API key never appears in logs
- **WHEN** any exception or informational log is emitted during enrichment
- **THEN** the log message does not contain the Azure OpenAI API key value

### Requirement: Check budget before each LLM call
The `EnrichmentAgent` SHALL call `TokenUsageTracker.EnforceBudget()` before invoking the Azure OpenAI model. If `EnforceBudget()` throws `BudgetExceededException`, the agent SHALL allow the exception to propagate immediately without retrying and without logging the full prompt.

#### Scenario: BudgetExceededException propagates before any LLM call is made
- **WHEN** `TokenUsageTracker.EnforceBudget()` throws `BudgetExceededException` at the start of a call
- **THEN** `EnrichAsync` propagates the exception and no request is sent to Azure OpenAI

#### Scenario: Budget check fires before retry attempts
- **WHEN** `EnforceBudget()` throws on the second attempt of a retry loop
- **THEN** no further LLM calls are made and `BudgetExceededException` propagates out of `EnrichAsync`

### Requirement: Report token usage after each successful LLM response
After receiving a successful `ChatMessageContent` response, the `EnrichmentAgent` SHALL extract `InputTokenCount` and `OutputTokenCount` from `response.Metadata["Usage"]` and pass them to `TokenUsageTracker.AddUsage()`. If the `"Usage"` key is absent or the cast fails, the agent SHALL call `AddUsage(0, 0)` and continue without throwing.

#### Scenario: Token counts extracted from metadata and reported
- **WHEN** the LLM returns a `ChatMessageContent` with valid usage metadata
- **THEN** `AddUsage` is called with the correct input and output token counts from that response

#### Scenario: Missing usage metadata does not fail enrichment
- **WHEN** `response.Metadata` does not contain the `"Usage"` key (e.g., in mock mode)
- **THEN** `AddUsage(0, 0)` is called and `EnrichAsync` completes normally
