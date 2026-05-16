## ADDED Requirements

### Requirement: Support --feature flag for hierarchy mode on a Feature
The `spec` command SHALL accept an optional `--feature` flag. When provided, `SpecCommand` SHALL enter hierarchy mode, treating the `<id>` argument as a Feature work item ID and running the hierarchy pipeline instead of the single-item pipeline. `--feature` and `--epic` SHALL be mutually exclusive; providing both SHALL result in a parse error before the handler runs.

#### Scenario: --feature triggers hierarchy pipeline
- **WHEN** `backlog-2-spec spec 1234 --feature` is called
- **THEN** the command fetches the Feature hierarchy, generates specs for all children, and writes output to `spec/1234-<slug>/`

#### Scenario: --feature and --epic together rejected at parse time
- **WHEN** `backlog-2-spec spec 1234 --feature --epic` is called
- **THEN** `System.CommandLine` rejects the combination and prints usage help before the handler runs

#### Scenario: --feature with non-Feature work item type shows warning
- **WHEN** `backlog-2-spec spec 1234 --feature` is called but work item 1234 is a PBI (not a Feature)
- **THEN** the command displays a warning "Work item 1234 is of type 'Product Backlog Item', not 'Feature'" and exits with code 1

### Requirement: Support --epic flag for hierarchy mode on an Epic
The `spec` command SHALL accept an optional `--epic` flag with identical semantics to `--feature` but validating that the fetched work item's type is "Epic".

#### Scenario: --epic triggers hierarchy pipeline for Epic type
- **WHEN** `backlog-2-spec spec 5000 --epic` is called and work item 5000 is an Epic
- **THEN** the command fetches the Epic's direct children (Features or PBIs) and writes specs to `spec/5000-<slug>/`

#### Scenario: --epic with non-Epic work item type shows warning
- **WHEN** `backlog-2-spec spec 5000 --epic` is called but work item 5000 is a Feature
- **THEN** the command displays a warning and exits with code 1

### Requirement: --mock flag supports hierarchy mode
When `--mock` and `--feature` (or `--epic`) are used together, `SpecCommand` SHALL use `MockAdoClient.GetWorkItemHierarchyAsync` to supply the hierarchy and run the full mock pipeline without any external calls.

#### Scenario: --mock --feature runs hierarchy pipeline without credentials
- **WHEN** `backlog-2-spec spec 99 --mock --feature` is called with no credentials configured
- **THEN** the command completes successfully, writes mock hierarchy spec files to `spec/99-<slug>/`, and exits with code 0
