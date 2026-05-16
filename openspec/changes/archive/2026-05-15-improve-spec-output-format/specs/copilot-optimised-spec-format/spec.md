## ADDED Requirements

### Requirement: GeneratedSpec uses Copilot-optimised field structure
The `GeneratedSpec` model SHALL contain exactly five fields in this order: `goal`, `behaviour`, `edgeCases`, `outOfScope`, `filesToChange`. The legacy fields `summary`, `acceptanceCriteria`, and `componentBreakdown` SHALL NOT exist.

- `goal`: 1–3 sentences. The first sentence states the capability being built. Subsequent sentences (optional) cover the user/system-visible outcome or a non-obvious constraint.
- `behaviour`: plain-English implementation bullets. Each bullet describes one thing the code must do, written from the developer's perspective, not a test perspective.
- `filesToChange`: file paths with a one-line description of what changes in each. The LLM SHALL resolve class names to paths using codebase context where available; when a path cannot be determined, it SHALL use the class name followed by `?`.

#### Scenario: GeneratedSpec contains exactly the five required fields
- **WHEN** a spec is generated for any work item
- **THEN** the returned object has `goal`, `behaviour`, `edgeCases`, `outOfScope`, and `filesToChange` — and no other fields

#### Scenario: goal is 1–3 sentences
- **WHEN** the spec is generated
- **THEN** `goal` is a non-empty string containing between 1 and 3 sentences

#### Scenario: behaviour contains plain-English bullets with no Gherkin syntax
- **WHEN** the spec is generated
- **THEN** no item in `behaviour` starts with `Given`, `When`, `Then`, or `Scenario:`

#### Scenario: filesToChange resolves to paths when codebase context is available
- **WHEN** codebase context includes a file containing the affected class
- **THEN** the corresponding `filesToChange` entry starts with the resolved file path, not just the class name

#### Scenario: filesToChange falls back to class name with ? when path is unknown
- **WHEN** codebase context is empty or does not contain the affected class
- **THEN** the corresponding `filesToChange` entry uses the class name followed by `?` (e.g., `AuthService?: add lockout logic`)
