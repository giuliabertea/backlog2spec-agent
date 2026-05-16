## ADDED Requirements

### Requirement: Write hierarchy output to files on disk
`IOutputRenderer` SHALL expose `WriteHierarchyToFiles(WorkItemDto parent, IEnumerable<(WorkItemDto Item, GeneratedSpec Spec)> children, string outputDir)`. The implementation SHALL:
- Create the `outputDir` directory (and any missing parent directories) if it does not exist.
- Write `_summary.md` containing the parent's metadata and a child index table.
- Write one `<childId>-<slug>.md` file per child, containing the `GeneratedSpec` rendered as markdown.
All files SHALL be UTF-8 encoded without BOM. Console output (progress, colors) SHALL be unaffected by this method.

#### Scenario: WriteHierarchyToFiles creates the output directory
- **WHEN** `WriteHierarchyToFiles` is called with an `outputDir` that does not exist
- **THEN** the directory is created before any file is written, and no exception is thrown

#### Scenario: _summary.md contains parent metadata
- **WHEN** `WriteHierarchyToFiles` is called with a Feature parent titled "User Management"
- **THEN** `_summary.md` begins with a heading containing "User Management" and includes the parent's ID, type, and description

#### Scenario: _summary.md contains child index
- **WHEN** `WriteHierarchyToFiles` is called with 3 children
- **THEN** `_summary.md` contains a markdown table or list with 3 rows, each showing the child ID and title

#### Scenario: Child spec file contains rendered GeneratedSpec
- **WHEN** `WriteHierarchyToFiles` is called with a child whose `GeneratedSpec.Summary` is "Handle user login"
- **THEN** the corresponding child file contains a markdown section with the text "Handle user login"

#### Scenario: Child file name is slugified
- **WHEN** a child has ID 1237 and title "User Login & Registration"
- **THEN** the file is written as `1237-user-login-registration.md` (special characters replaced by hyphens, lowercased)

#### Scenario: Files are UTF-8 without BOM
- **WHEN** any file is written by `WriteHierarchyToFiles`
- **THEN** the file bytes do not begin with the UTF-8 BOM sequence (0xEF, 0xBB, 0xBF)
