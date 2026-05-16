## Why

LLM-generated specs and enrichments have no awareness of project-specific conventions (naming patterns, forbidden libraries, architecture constraints, coding standards). Adding a `devRulesFile` field to `backlog-2-spec.json` lets teams codify those rules once in a markdown file and have them automatically injected into every prompt, making output consistent with the real project's standards without manual prompt engineering.

## What Changes

- `backlog-2-spec.json` gains an optional `devRulesFile` field: a relative or absolute path to a markdown file containing project development rules
- `ConfigLoader` reads the field, resolves the path relative to the config file location, and loads the file content into `AgentConfig`; if the field is set but the file does not exist, a `ConfigException` is thrown
- `EnrichmentAgent` injects the dev rules content under a `## Development Rules` section of the enrichment prompt when present
- `SpecGeneratorAgent` injects the dev rules content under a `## Development Rules` section of the spec generation prompt when present

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `config-loader`: new optional field `devRulesFile`; file resolution and content loading into `AgentConfig`; throws `ConfigException` if path is set but file is missing
- `enrichment-agent`: inject `## Development Rules` section into prompt when `AgentConfig.DevRulesContent` is non-empty
- `spec-generator-agent`: inject `## Development Rules` section into prompt when `AgentConfig.DevRulesContent` is non-empty

## Impact

- `AgentConfig` gains a new nullable string property `DevRulesContent` (populated by `ConfigLoader`)
- `backlog-2-spec.json` schema gains optional `devRulesFile` field
- No breaking changes — the field is optional and existing configs without it continue to work unchanged
- Affects `EnrichmentAgent` and `SpecGeneratorAgent` prompt construction; mock implementations do not need changes
