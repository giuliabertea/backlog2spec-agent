## MODIFIED Requirements

### Requirement: Render GeneratedSpec with color-coded sections
`OutputRenderer` SHALL render the `GeneratedSpec` with the following Spectre.Console color scheme:
- Section headers: blue
- Body text (Goal, OutOfScope, FilesToChange): white
- Behaviour bullets: white
- Edge cases: yellow
- Ambiguities (if shown in verbose mode): red

The rendered sections SHALL use these headings in order: Goal, Behaviour, Edge Cases, Out of Scope, Files to Change.

#### Scenario: Goal header rendered in blue
- **WHEN** `RenderSpec(spec)` is called
- **THEN** the "Goal" header text is rendered in blue and the goal text is rendered in white

#### Scenario: Behaviour section renders plain-English bullets
- **WHEN** `RenderSpec(spec)` is called and `Behaviour` is non-empty
- **THEN** each behaviour item is rendered as a bullet in white with no Gherkin code blocks

#### Scenario: Edge cases rendered in yellow
- **WHEN** `RenderSpec(spec)` is called and `EdgeCases` is non-empty
- **THEN** each edge case item is rendered in yellow

#### Scenario: Files to Change section renders path-prefixed entries
- **WHEN** `RenderSpec(spec)` is called and `FilesToChange` is non-empty
- **THEN** each entry is rendered with the path portion in bold and the description portion in normal weight

#### Scenario: Markdown output uses correct section headings
- **WHEN** `RenderMarkdown(spec, ...)` is called
- **THEN** the output file contains `## Goal`, `## Behaviour`, `## Edge Cases`, `## Out of Scope`, `## Files to Change` sections in that order, and no `## Summary`, `## Acceptance Criteria`, or `## Component Breakdown` headings

#### Scenario: Verbose mode shows ambiguities in red
- **WHEN** `RenderSpec(spec, verbose: true)` is called and ambiguities are present
- **THEN** each ambiguity is rendered in red
