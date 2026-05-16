## 1. Models

- [x] 1.1 Add `WorkItemHierarchyDto` record with `WorkItemDto Parent` and `IReadOnlyList<WorkItemDto> Children` to the `Models/` folder

## 2. ADO Client — Hierarchy Fetch

- [x] 2.1 Add `GetWorkItemHierarchyAsync(int parentId, CancellationToken ct)` to `IAdoClient`
- [x] 2.2 Implement `GetWorkItemHierarchyAsync` in `AdoClient`: fetch parent with `WorkItemExpand.Relations`, extract `System.LinkTypes.Hierarchy-Forward` child IDs from relation URLs, batch-fetch children with `GetWorkItemsAsync(ids, expand: WorkItemExpand.All)`
- [x] 2.3 Implement `GetWorkItemHierarchyAsync` in `MockAdoClient` returning a hard-coded `WorkItemHierarchyDto` with a mock Feature parent and two mock child `WorkItemDto` entries

## 3. Output Renderer — File Writing

- [x] 3.1 Add `WriteHierarchyToFiles(WorkItemDto parent, IEnumerable<(WorkItemDto Item, GeneratedSpec Spec)> children, string outputDir)` to `IOutputRenderer`
- [x] 3.2 Implement `WriteHierarchyToFiles` in `OutputRenderer`: create `outputDir` if missing, write `_summary.md` (parent metadata + child index table), write one `<childId>-<slug>.md` per child with `GeneratedSpec` rendered as markdown; all files UTF-8 without BOM
- [x] 3.3 Add a `Slugify(string title, int maxLength = 60)` helper (lowercase, replace non-alphanumeric with hyphens, trim leading/trailing hyphens)

## 4. CLI Flags — SpecCommand

- [x] 4.1 Add `--feature` and `--epic` `Option<bool>` to `SpecCommand` and register them as mutually exclusive (use `AddValidator` or a custom validator on the command)
- [x] 4.2 Wire the new flags into `SpecCommand.SetHandler` and pass `isFeature`/`isEpic` to `ExecuteAsync`

## 5. Hierarchy Pipeline — SpecCommand Orchestration

- [x] 5.1 Add a `ExecuteHierarchyAsync(int id, bool isFeature, bool isEpic, bool raw, bool mock, CancellationToken ct)` branch in `SpecCommand` (or inline in `ExecuteAsync` with an if-branch)
- [x] 5.2 Validate that the fetched parent's `WorkItemType` matches the flag (`--feature` → "Feature", `--epic` → "Epic"); display a warning and return exit code 1 on mismatch
- [x] 5.3 Fetch codebase context once for the parent, then loop over children: enrich each child, generate its spec, collect `(WorkItemDto, GeneratedSpec)` pairs; log a warning and skip on per-child failure
- [x] 5.4 Show per-child progress via `_renderer.RenderProgress($"Generating spec for #{childId} ({i}/{total})...")` unless `--raw`
- [x] 5.5 After all children are processed, call `_renderer.WriteHierarchyToFiles(parent, results, outputDir)` to write the folder
- [x] 5.6 Return exit code 0 if at least one child succeeded; return exit code 1 (with summary) if all children failed

## 6. Verification

- [x] 6.1 Run `backlog-2-spec spec 99 --mock --feature` and confirm `spec/99-mock-feature/` is created with `_summary.md` and at least two child spec files
- [x] 6.2 Confirm existing `backlog-2-spec spec 99 --mock` still works and writes only to console (no files created)
- [x] 6.3 Confirm `backlog-2-spec spec 99 --feature --epic` prints a parse error and exits non-zero
- [x] 6.4 Build the project with no compiler errors or warnings
