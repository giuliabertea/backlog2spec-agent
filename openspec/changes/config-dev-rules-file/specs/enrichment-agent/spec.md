## ADDED Requirements

### Requirement: Inject development rules into enrichment prompt
When `AgentConfig.DevRulesContent` is non-null and non-empty, `EnrichmentAgent` SHALL inject its content under a `## Development Rules` section in the enrichment prompt. The model SHALL be instructed to apply those rules when inferring components, constraints, and acceptance criteria. When `DevRulesContent` is null or empty, the `{{devRules}}` placeholder SHALL resolve to an empty string, producing no section in the prompt.

#### Scenario: DevRulesContent present injects section into prompt
- **WHEN** `AgentConfig.DevRulesContent` contains a non-empty string and `EnrichAsync` is called
- **THEN** the built prompt contains a `## Development Rules` section with that content before the LLM call

#### Scenario: DevRulesContent absent produces no section in prompt
- **WHEN** `AgentConfig.DevRulesContent` is null
- **THEN** the built prompt does not contain a `## Development Rules` section and no empty placeholder remains
