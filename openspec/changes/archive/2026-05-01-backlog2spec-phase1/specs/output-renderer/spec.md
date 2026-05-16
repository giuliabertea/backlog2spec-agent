## ADDED Requirements

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
- Body text (Summary, OutOfScope, ComponentBreakdown): white
- Edge cases: yellow
- Ambiguities (if shown in verbose mode): red

#### Scenario: Summary header rendered in blue
- **WHEN** `RenderSpec(spec)` is called
- **THEN** the "Summary" header text is rendered in blue

#### Scenario: Edge cases rendered in yellow
- **WHEN** `RenderSpec(spec)` is called and `EdgeCases` is non-empty
- **THEN** each edge case item is rendered in yellow

#### Scenario: Verbose mode shows ambiguities in red
- **WHEN** `RenderSpec(spec, verbose: true)` is called and ambiguities are present
- **THEN** each ambiguity is rendered in red

### Requirement: Render typed error messages cleanly
`OutputRenderer` SHALL expose a `RenderError(string message)` method that displays the error in red to the terminal. It SHALL NOT display stack traces.

#### Scenario: Error message displayed in red without stack trace
- **WHEN** `RenderError("Work item 42 not found.")` is called
- **THEN** the terminal shows the message in red with no additional technical detail
