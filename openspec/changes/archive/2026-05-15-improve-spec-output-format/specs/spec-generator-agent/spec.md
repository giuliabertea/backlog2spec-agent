## MODIFIED Requirements

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
