## Why

The current spec output uses Gherkin acceptance criteria (Given/When/Then) and class-name-only component breakdowns, which are verbose in tokens and poorly suited for AI coding assistants like GitHub Copilot. Copilot needs file paths, concise behaviour descriptions, and a clear goal — not a BDD test format.

## What Changes

- Replace Gherkin `acceptanceCriteria` with a `behaviour` field: plain-English bullets describing what the implementation must do
- Replace `componentBreakdown` (class names + responsibility) with a `filesToChange` field: actual file paths with a one-line description of what changes in each file
- Replace the 2-3 sentence `summary` with a `goal` field: 1 mandatory sentence (the capability) + up to 2 optional sentences (outcome and non-obvious constraints)
- Retain `edgeCases` and `outOfScope` unchanged — these are already well-suited for Copilot consumption

## Capabilities

### New Capabilities
- `copilot-optimised-spec-format`: The generated spec output structure (JSON model, LLM prompt, markdown renderer) uses goal + filesToChange + behaviour instead of summary + acceptanceCriteria + componentBreakdown

### Modified Capabilities
- `spec-generator-agent`: The JSON schema it produces changes — output fields renamed and restructured
- `output-renderer`: Markdown and console rendering updated to match the new field names and sections

## Impact

- `src/Backlog2SpecAgent.Cli/Models/GeneratedSpec.cs` — field renames/additions
- `src/Backlog2SpecAgent.Cli/Prompts/spec.txt` — new JSON schema, rules, and example
- `src/Backlog2SpecAgent.Cli/Agents/SpecGeneratorAgent.cs` — template variable for `affectedComponents` → `filesToChange` in output schema
- `src/Backlog2SpecAgent.Cli/Output/OutputRenderer.cs` — updated section rendering for all output modes (console, markdown, hierarchy)
- No breaking changes to CLI flags or config schema
