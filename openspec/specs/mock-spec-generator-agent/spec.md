# mock-spec-generator-agent

## Purpose

`MockSpecGeneratorAgent` is a test-only implementation of `ISpecGeneratorAgent` that returns a fixed, hardcoded `GeneratedSpec` without making any external calls. It is used when `--mock` mode is active to enable full pipeline execution without Azure OpenAI credentials.

## Requirements

### Requirement: Provide deterministic spec generation without LLM calls
`MockSpecGeneratorAgent` SHALL implement `ISpecGeneratorAgent` and return a fixed, hardcoded `GeneratedSpec` for any input. It SHALL make no external calls (no HTTP, no Azure OpenAI). The returned data SHALL be identical on every invocation regardless of the input `EnrichedTicket` or `AgentConfig`.

#### Scenario: Returns fixed spec data
- **WHEN** `GenerateAsync` is called with any enriched ticket
- **THEN** the returned `GeneratedSpec` has all fields populated with exactly the hardcoded values defined in the implementation

#### Scenario: Makes no external network calls
- **WHEN** `GenerateAsync` is called with no network access available
- **THEN** the method completes successfully and returns the fixed `GeneratedSpec`

#### Scenario: Summary is the hardcoded mock string
- **WHEN** `GenerateAsync` is called
- **THEN** `GeneratedSpec.Summary` equals `"Mock spec for testing pipeline"`

#### Scenario: AcceptanceCriteria contains two Gherkin entries
- **WHEN** `GenerateAsync` is called
- **THEN** `GeneratedSpec.AcceptanceCriteria` contains exactly `["Given valid input, When processed, Then a success response is returned", "Given invalid input, When processed, Then an error is returned"]`

#### Scenario: EdgeCases contains two hardcoded entries
- **WHEN** `GenerateAsync` is called
- **THEN** `GeneratedSpec.EdgeCases` contains exactly `["Null input", "Extremely large payload"]`

#### Scenario: OutOfScope is the hardcoded mock string
- **WHEN** `GenerateAsync` is called
- **THEN** `GeneratedSpec.OutOfScope` equals `"Authentication, Authorization"`

#### Scenario: ComponentBreakdown contains three hardcoded entries
- **WHEN** `GenerateAsync` is called
- **THEN** `GeneratedSpec.ComponentBreakdown` contains exactly `["API Endpoint", "Application Service", "Validation Layer"]`
