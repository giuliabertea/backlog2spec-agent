## MODIFIED Requirements

### Requirement: Support --mock flag for pipeline execution without external calls
The `spec` command SHALL accept an optional `--mock` flag. When enabled, the command SHALL use mock implementations (`MockAdoClient`, `MockEnrichmentAgent`, `MockSpecGeneratorAgent`) instead of real ones. All external calls (Azure DevOps, Azure OpenAI) SHALL be skipped. Output SHALL still route through `OutputRenderer` with the same formatting as real mode. The process SHALL log `[MOCK MODE ENABLED]` at Information level when this flag is active.

#### Scenario: --mock runs full pipeline without credentials
- **WHEN** `backlog-2-spec spec 99 --mock` is called with no secrets configured
- **THEN** the command completes successfully, renders the mock spec to the terminal, and exits with code 0

#### Scenario: --mock output uses OutputRenderer with normal formatting
- **WHEN** `backlog-2-spec spec 99 --mock` is called
- **THEN** the rendered output uses the same headers, colors, and layout as a real run

#### Scenario: --mock combined with --verbose shows verbose detail
- **WHEN** `backlog-2-spec spec 99 --mock --verbose` is called
- **THEN** the enrichment detail section is rendered in addition to the generated spec

#### Scenario: --mock combined with --raw outputs JSON only
- **WHEN** `backlog-2-spec spec 99 --mock --raw` is called
- **THEN** stdout contains only valid JSON representing the mock `GeneratedSpec`

#### Scenario: Without --mock real implementations are used
- **WHEN** `backlog-2-spec spec <id>` is called without `--mock`
- **THEN** the real `AdoClient`, `EnrichmentAgent`, and `SpecGeneratorAgent` are used as before
