# output-renderer

## Purpose

`OutputRenderer` is the sole class responsible for all console output in the system. It uses Spectre.Console to render progress steps, color-coded spec sections, and error messages. All other classes are free of direct console writes and depend on `OutputRenderer` via its interface.

## Requirements

### Requirement: OutputRenderer is the sole class that writes to the console
`OutputRenderer` SHALL be the only class in the system that calls Spectre.Console or any other console output API. All other classes (agents, command, client, loader) SHALL be free of direct console writes. `OutputRenderer` SHALL be injected via its interface wherever output is needed.

#### Scenario: No console writes outside OutputRenderer
- **WHEN** any class other than `OutputRenderer` is inspected
- **THEN** it contains no calls to `Console.Write`, `Console.WriteLine`, `AnsiConsole`, or equivalent

### Requirement: Show progress steps during pipeline execution
`OutputRenderer` SHALL display three labeled progress steps via Spectre.Console: "Fetching work item", "Enriching ticket", and "Generating spec". Each step SHALL update visually as the pipeline advances.

#### Scenario: Progress steps appear in sequence
- **WHEN** `backlog-2-spec spec <id>` is executed
- **THEN** the terminal shows "Fetching work item", then "Enriching ticket", then "Generating spec" as each stage begins

#### Scenario: Progress steps not shown in --raw mode
- **WHEN** `backlog-2-spec spec <id> --raw` is executed
- **THEN** no progress steps are rendered to stdout

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

### Requirement: Render typed error messages cleanly
`OutputRenderer` SHALL expose a `RenderError(string message)` method that displays the error in red to the terminal. It SHALL NOT display stack traces.

#### Scenario: Error message displayed in red without stack trace
- **WHEN** `RenderError("Work item 42 not found.")` is called
- **THEN** the terminal shows the message in red with no additional technical detail
