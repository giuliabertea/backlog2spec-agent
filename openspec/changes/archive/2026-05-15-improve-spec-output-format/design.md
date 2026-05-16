## Context

The spec output pipeline has three layers: the `GeneratedSpec` C# model (the JSON contract between LLM and app), the `spec.txt` LLM prompt (defines the JSON schema and example the LLM follows), and `OutputRenderer` (renders the model to console and markdown). All three must change together.

The enrichment agent (`EnrichedTicket`) is upstream and **not changing** — it still produces `affectedComponents` as class/service names. The spec generator LLM already receives the full codebase context (file paths + content) via `{{codebaseContext}}`, so it has the information needed to resolve class names to file paths in its output.

## Goals / Non-Goals

**Goals:**
- Rename `summary` → `goal`, `acceptanceCriteria` → `behaviour`, `componentBreakdown` → `filesToChange` in the model and everywhere downstream
- `goal`: 1-3 sentences (mandatory outcome sentence + up to 2 for non-obvious constraints or integrations)
- `behaviour`: plain-English implementation bullets, no Gherkin syntax
- `filesToChange`: file paths (not just class names) with a one-line description of what changes — resolved from codebase context by the LLM
- Keep `edgeCases` and `outOfScope` unchanged

**Non-Goals:**
- Changing the enrichment agent or `EnrichedTicket` model
- Changing CLI flags, config schema, or the `--output` path logic
- Generating separate "test spec" vs "implementation spec" outputs (deferred)

## Decisions

### 1. Rename fields in-place rather than adding new fields alongside old ones

Alternatives considered:
- Add `goal`, `behaviour`, `filesToChange` as new fields and keep the old ones as deprecated → adds dead weight to the model, the prompt, and the renderer; no stored spec data exists so there is nothing to migrate
- Version the output schema (e.g. `"version": 2`) → unnecessary complexity for a CLI tool with no persistence

**Decision**: Rename in-place. All three layers change in one go. No migration needed.

### 2. LLM resolves class names to file paths using codebase context

The enrichment agent outputs `affectedComponents` as names like `"AuthService"`. The spec generator LLM receives the full file paths and contents in `{{codebaseContext}}`. It is better positioned than any deterministic code to match class names to paths.

Alternatives considered:
- Add a deterministic lookup step in `SpecGeneratorAgent.BuildPrompt` to resolve paths before building the prompt → brittle (naming conventions vary), adds complexity
- Ask the enrichment agent to output paths directly → enrichment runs before codebase context is filtered to relevant files; it would need access to the full file index

**Decision**: Instruct the LLM in `spec.txt` to resolve component names to paths using the provided codebase context. When a path cannot be determined, use the class name with a `?` suffix so the output remains useful.

### 3. `filesToChange` format: `"path/to/File.cs: what changes"`

Same colon-separated string format as the current `componentBreakdown`. The renderer already handles this format (splits on first colon to bold the path). No renderer logic change needed beyond the section heading.

### 4. `behaviour` bullets: implementation-facing, not test-facing

Each bullet describes a code behaviour ("Increment failed attempt counter on wrong password"), not an observable test outcome. This gives Copilot actionable signal about what to write, not how to verify it.

## Risks / Trade-offs

- **Sparse codebase context → LLM falls back to class names** — when no relevant files are found, `filesToChange` will contain class names rather than paths. This is acceptable; the output degrades gracefully. Mitigation: the `?` suffix convention makes fallback items visually distinct.
- **Behaviour bullets may be too terse for complex tickets** — plain bullets lose the structured Given/When/Then flow that helps with multi-step scenarios. Mitigation: the field allows any number of bullets; the LLM prompt will instruct it to add one bullet per distinct behaviour, not one bullet total.
- **Breaking change for any downstream tooling that parses the JSON output** — consumers of `--output` JSON files will see field name changes. Mitigation: this is a CLI tool; no known external consumers. The change is intentional and scoped.

## Open Questions

- ~~Should the markdown renderer use a `filesToChange` code block or a plain list?~~ **Resolved:** plain list with bold path (`- **path**: description`), consistent with the existing `componentBreakdown` style and readable in GitHub/ADO markdown preview. The same bold-path pattern is applied to the console renderer using Spectre.Console markup.
