## ADDED Requirements

### Requirement: Generate a folder of spec files for a Feature or Epic
When hierarchy mode is active, `SpecCommand` SHALL orchestrate the following pipeline:
1. Fetch `WorkItemHierarchyDto` from `IAdoClient.GetWorkItemHierarchyAsync`.
2. Write a summary file `_summary.md` for the parent to the output folder.
3. For each child work item: run the full enrich → generate pipeline and write a spec file `<child-id>-<slug>.md` to the output folder.
The output folder path SHALL be `spec/<parentId>-<slug>/` relative to the current working directory, where `<slug>` is the parent title lowercased with all non-alphanumeric characters replaced by hyphens, truncated to 60 characters.

#### Scenario: Feature with children produces one folder and N+1 files
- **WHEN** `backlog-2-spec spec 1234 --feature` is called and the Feature has 3 child PBIs
- **THEN** the directory `spec/1234-<slug>/` is created, containing `_summary.md` and 3 child spec files (e.g., `1235-child-one.md`, `1236-child-two.md`, `1237-child-three.md`)

#### Scenario: Output folder is created if it does not exist
- **WHEN** the `spec/` directory does not exist before the command runs
- **THEN** the command creates both `spec/` and `spec/<parentId>-<slug>/` without error

#### Scenario: Existing files in output folder are overwritten
- **WHEN** `spec/<parentId>-<slug>/` already exists from a previous run
- **THEN** files with the same names are overwritten; no error is raised

### Requirement: Summary file contains parent metadata and child index
The `_summary.md` file written for the parent work item SHALL contain: the work item ID, title, type (Feature or Epic), description, and an ordered list of child work item IDs and titles as a markdown table or bullet list. It SHALL NOT invoke the AI pipeline for the parent item.

#### Scenario: Summary file lists all children
- **WHEN** `_summary.md` is written for a Feature with 3 children
- **THEN** the file contains a section listing all 3 child IDs and titles

#### Scenario: Summary file includes parent description
- **WHEN** the parent Feature has a non-empty `Description` field
- **THEN** `_summary.md` includes that description in plain text (HTML already stripped by AdoClient)

### Requirement: Child spec files are generated using the full pipeline
Each child spec file SHALL be produced by running the existing enrich → generate pipeline (`IEnrichmentAgent`, `ISpecGeneratorAgent`) for that child's `WorkItemDto`, then rendered to a markdown file via `IOutputRenderer.WriteHierarchyToFiles`. The codebase context SHALL be fetched once for the parent and reused across all children.

#### Scenario: Child spec file contains generated spec content
- **WHEN** a child PBI with title "User login" is processed
- **THEN** the corresponding child spec file contains the `GeneratedSpec` fields (Summary, OutOfScope, EdgeCases, etc.) rendered as markdown

#### Scenario: Codebase context is fetched once, not per child
- **WHEN** hierarchy mode runs with 5 children
- **THEN** `ICodebaseContextAgent.FetchRelevantFilesAsync` is called exactly once (for the parent), and the result is reused for all children's enrichment calls

### Requirement: Failed children are skipped with a warning, not aborted
If any child work item fails to enrich or generate (LLM error, missing fields, etc.), the command SHALL log a warning identifying the failed child ID, skip that file, and continue processing remaining children. After all children are processed, the command SHALL report a summary of how many succeeded and how many were skipped. The process SHALL still exit with code 0 if at least one child succeeded; it SHALL exit with code 1 only if all children failed.

#### Scenario: One failed child does not abort the run
- **WHEN** child #1236 throws `LlmFormatException` during generation
- **THEN** the spec files for #1235 and #1237 are still written, a warning for #1236 is shown, and the process exits with code 0

#### Scenario: All children failed exits with code 1
- **WHEN** every child throws an exception during the generate step
- **THEN** no child spec files are written, an error summary is shown, and the process exits with code 1

### Requirement: Progress feedback during hierarchy pipeline
The command SHALL show a labeled progress step for each child as it is processed: e.g., "Generating spec for #1236 (2/3)...". This SHALL use the existing `IOutputRenderer.RenderProgress` method.

#### Scenario: Progress step shown per child
- **WHEN** `backlog-2-spec spec 1234 --feature` is called with 3 children (and not `--raw`)
- **THEN** three progress messages appear in sequence, each identifying the current child ID and position in the total
