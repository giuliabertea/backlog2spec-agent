## Context

Both `EnrichmentAgent` and `SpecGeneratorAgent` build prompts via string template replacement (`{{placeholder}}`). The templates are loaded from disk at agent construction time and placeholders are filled from `AgentConfig` and `WorkItemDto`. Currently there is no way to inject arbitrary project-level guidance — the `ConventionsConfig` fields exist but are short strings, not rich markdown content loaded from a separate file.

`AgentConfig` is a plain POCO deserialized by `ConfigLoader` using `System.Text.Json`. Its properties use `init` accessors, making them immutable after construction. `ConfigLoader.LoadAsync()` is the single point that reads, deserializes, and validates the config.

## Goals / Non-Goals

**Goals:**
- Allow `backlog-2-spec.json` to reference a markdown file whose content gets injected into both agents' prompts
- Fail fast with a clear error if the file is referenced but not found
- Zero impact when the field is absent (existing configs unchanged)

**Non-Goals:**
- Validating the *content* of the rules file (any markdown is accepted)
- Supporting multiple rules files or glob patterns
- Injecting rules into mock agents or test stubs
- Hot-reloading the file between runs

## Decisions

**1. Where to store the loaded content in `AgentConfig`**

The JSON-deserialized `DevRulesFile` field (nullable string) lives on `AgentConfig` and is set via `init`. After deserialization, `ConfigLoader` reads the file and needs to attach the content. Since `init` properties can only be set at construction, `DevRulesContent` uses `{ get; internal set; }` — a deliberate exception because it is computed post-deserialization by `ConfigLoader`, not supplied by the JSON consumer.

Alternative considered: return a wrapper type from `LoadAsync`. Rejected — it changes the public API surface and forces callers to unwrap.

**2. Path resolution**

`devRulesFile` is resolved relative to the directory containing `backlog-2-spec.json` (not the CWD). This matches how most config-relative paths work and allows the file to live alongside the config without requiring an absolute path.

**3. Prompt injection strategy**

Add a `{{devRules}}` placeholder to both `enrichment.txt` and `spec.txt` prompt templates. In `BuildPrompt`, replace it with either:
- The full file content (when `DevRulesContent` is non-null and non-empty), wrapped in a `## Development Rules` markdown section
- An empty string (when `DevRulesContent` is absent)

This keeps the prompt templates as the single source of truth for structure, and avoids embedding conditional logic inside the template files.

Alternative considered: append the rules block after the full prompt string. Rejected — harder to control placement relative to other sections (e.g., codebase context).

**4. No changes to mock agents**

`MockEnrichmentAgent` and `MockSpecGeneratorAgent` return hardcoded responses and do not call `BuildPrompt`. No changes needed there.

## Risks / Trade-offs

- **Large rules files inflate token usage** → The file content is injected verbatim; users are responsible for keeping it concise. No truncation is applied by the tool.
- **`internal set` on `AgentConfig.DevRulesContent`** breaks strict immutability of the config POCO → Acceptable given that `ConfigLoader` is the only writer and lives in the same assembly.

## Migration Plan

No migration needed. The `devRulesFile` field is optional. Existing `backlog-2-spec.json` files without it continue to work without modification. The `{{devRules}}` placeholder replaces to an empty string when absent, producing identical prompt output to the current behavior.
