## ADDED Requirements

### Requirement: Provide deterministic enrichment without LLM calls
`MockEnrichmentAgent` SHALL implement `IEnrichmentAgent` and return a fixed, hardcoded `EnrichedTicket` for any input. It SHALL make no external calls (no HTTP, no Azure OpenAI). The returned data SHALL be identical on every invocation regardless of the input `WorkItemDto` or `AgentConfig`.

#### Scenario: Returns fixed enrichment data
- **WHEN** `EnrichAsync` is called with any work item
- **THEN** the returned `EnrichedTicket` has `WorkItemId` set from the input's `Id`, `Title` set from the input's `Title`, and all list fields populated with exactly the hardcoded values defined in the implementation

#### Scenario: Makes no external network calls
- **WHEN** `EnrichAsync` is called with no network access available
- **THEN** the method completes successfully and returns the fixed `EnrichedTicket`

#### Scenario: MissingAcceptanceCriteria contains one hardcoded entry
- **WHEN** `EnrichAsync` is called
- **THEN** `EnrichedTicket.MissingAcceptanceCriteria` contains exactly `["Validation rules not defined"]`

#### Scenario: EdgeCases contains three hardcoded entries
- **WHEN** `EnrichAsync` is called
- **THEN** `EnrichedTicket.EdgeCases` contains exactly `["Null input", "Large payload", "Timeout handling"]`

#### Scenario: Constraints contains two hardcoded entries
- **WHEN** `EnrichAsync` is called
- **THEN** `EnrichedTicket.Constraints` contains exactly `["Must support .NET 8", "REST API only"]`

#### Scenario: AffectedComponents contains two hardcoded entries
- **WHEN** `EnrichAsync` is called
- **THEN** `EnrichedTicket.AffectedComponents` contains exactly `["API", "Application Layer"]`

#### Scenario: Ambiguities contains one hardcoded entry
- **WHEN** `EnrichAsync` is called
- **THEN** `EnrichedTicket.Ambiguities` contains exactly `["Expected max payload size unknown"]`
