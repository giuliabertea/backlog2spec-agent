## ADDED Requirements

### Requirement: Generate structured spec from enriched ticket
The `SpecGeneratorAgent` SHALL accept an `EnrichedTicket` and `AgentConfig`, invoke the Azure OpenAI model via Semantic Kernel, and return a `GeneratedSpec`. The `GeneratedSpec` SHALL always contain exactly these sections in this order: `Summary`, `AcceptanceCriteria` (Gherkin), `EdgeCases`, `OutOfScope`, `ComponentBreakdown`. No additional sections SHALL be added.

#### Scenario: Valid enriched ticket produces fully populated GeneratedSpec
- **WHEN** `GenerateAsync(enrichedTicket, config)` is called with a complete `EnrichedTicket`
- **THEN** the returned `GeneratedSpec` has non-empty `Summary`, `AcceptanceCriteria`, `EdgeCases`, `OutOfScope`, and `ComponentBreakdown` properties

#### Scenario: AcceptanceCriteria formatted as Gherkin
- **WHEN** the `GeneratedSpec` is returned
- **THEN** each item in `AcceptanceCriteria` is a Gherkin-style scenario string starting with `Given`, `When`, or `Scenario:`

#### Scenario: Section ordering is always stable
- **WHEN** `GenerateAsync` is called multiple times with the same input
- **THEN** the `GeneratedSpec` always has the same section order: Summary, AcceptanceCriteria, EdgeCases, OutOfScope, ComponentBreakdown

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
