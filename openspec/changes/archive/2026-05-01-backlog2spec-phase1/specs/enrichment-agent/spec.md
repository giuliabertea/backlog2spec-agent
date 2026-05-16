## ADDED Requirements

### Requirement: Enrich work item via Azure OpenAI
The `EnrichmentAgent` SHALL accept a `WorkItemDto` and `AgentConfig`, invoke the Azure OpenAI model via Semantic Kernel, and return a populated `EnrichedTicket`. The prompt SHALL include the full JSON schema for `EnrichedTicket` and one example input/output pair. The prompt SHALL instruct the model not to guess — if information is not derivable from the work item, the relevant list SHALL be empty.

#### Scenario: Well-defined work item produces all enrichment sections
- **WHEN** `EnrichAsync(workItem, config)` is called with a work item that has title, description, and acceptance criteria
- **THEN** the returned `EnrichedTicket` has non-empty `MissingAcceptanceCriteria`, `EdgeCases`, `Constraints`, `AffectedComponents`, and `Ambiguities` lists where information is inferable

#### Scenario: Sparse work item produces partial enrichment without exception
- **WHEN** `EnrichAsync(workItem, config)` is called with a work item that has only a title
- **THEN** the returned `EnrichedTicket` has empty lists for sections that cannot be inferred, and the call completes without throwing

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
