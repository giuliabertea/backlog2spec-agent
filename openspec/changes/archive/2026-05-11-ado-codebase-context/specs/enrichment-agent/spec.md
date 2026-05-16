## MODIFIED Requirements

### Requirement: Enrich work item via Azure OpenAI
The `EnrichmentAgent` SHALL accept a `WorkItemDto`, an `AgentConfig`, and an `IReadOnlyList<CodeFileDto> codebaseContext`, invoke the Azure OpenAI model via Semantic Kernel, and return a populated `EnrichedTicket`. The prompt SHALL include the full JSON schema for `EnrichedTicket` and one example input/output pair. The prompt SHALL instruct the model not to guess — if information is not derivable from the work item, the relevant list SHALL be empty.

#### Scenario: Well-defined work item produces all enrichment sections
- **WHEN** `EnrichAsync(workItem, config, codebaseContext)` is called with a work item that has title, description, and acceptance criteria
- **THEN** the returned `EnrichedTicket` has non-empty `MissingAcceptanceCriteria`, `EdgeCases`, `Constraints`, `AffectedComponents`, and `Ambiguities` lists where information is inferable

#### Scenario: Sparse work item produces partial enrichment without exception
- **WHEN** `EnrichAsync(workItem, config, codebaseContext)` is called with a work item that has only a title
- **THEN** the returned `EnrichedTicket` has empty lists for sections that cannot be inferred, and the call completes without throwing

## ADDED Requirements

### Requirement: Include codebase context in enrichment prompt
When `codebaseContext` is non-empty, `EnrichmentAgent` SHALL format each `CodeFileDto` as a labeled snippet (`File: {Path}\n---\n{Content}`) and inject them under the `## Codebase Context` section of the enrichment prompt. When `codebaseContext` is empty, the section SHALL contain the literal text `"No codebase context available."`. The prompt SHALL instruct the model to prefer component names found in the codebase snippets over generic guesses.

#### Scenario: Non-empty context injects file snippets into prompt
- **WHEN** `codebaseContext` contains one or more `CodeFileDto` entries
- **THEN** the built prompt contains a `## Codebase Context` section with each file's path and content

#### Scenario: Empty context inserts placeholder text
- **WHEN** `codebaseContext` is an empty list
- **THEN** the built prompt contains `"No codebase context available."` under `## Codebase Context`
