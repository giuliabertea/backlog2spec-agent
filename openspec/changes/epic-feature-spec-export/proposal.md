## Why

Today `backlog-2-spec spec <id>` only handles a single PBI, forcing users to run the command once per item when converting a whole Feature or Epic. This change adds hierarchical export so teams can turn an entire Epic or Feature — plus all its child PBIs — into a structured folder of spec files in one command.

## What Changes

- Add `--feature` and `--epic` flags to the existing `spec` command (or accept a work item ID of the right type automatically) that switch the command into **hierarchy mode**.
- In hierarchy mode, fetch the target work item (Feature or Epic) from ADO **and all its direct children** (PBIs / User Stories).
- Generate a `spec/` subfolder named after the work item title (slugified), containing:
  - `_feature.md` (or `_epic.md`) — summary file with the parent item's fields and an index of children.
  - `<child-id>-<slug>.md` — one spec file per child work item, generated the same way as the current single-PBI flow.
- The `AdoClient` gains a method to fetch a work item **with its children** via the ADO hierarchy/relations API.
- The `OutputRenderer` gains a file-writing mode (writes markdown files to disk instead of printing to console).

## Capabilities

### New Capabilities
- `ado-hierarchy-fetch`: Fetches a parent work item (Feature or Epic) and its direct child work items from ADO in a single operation, returning a typed `WorkItemHierarchyDto`.
- `hierarchy-spec-export`: Orchestrates the full hierarchy pipeline — fetch parent + children, generate a summary and per-child specs, and write all files to `spec/<slug>/`.

### Modified Capabilities
- `spec-command`: Adds `--feature` and `--epic` flags that trigger hierarchy mode instead of the single-item pipeline.
- `output-renderer`: Adds file-writing capability used in hierarchy mode (`RenderHierarchyToFiles`), keeping console rendering untouched for the existing single-PBI flow.

## Impact

- **CLI surface**: two new optional flags on `backlog-2-spec spec`; existing usage unchanged.
- **ADO API**: new call to fetch work item relations/children (same PAT auth, same `AgentConfig`).
- **File system**: hierarchy mode writes files under `spec/` in the current working directory; single-item mode still writes to console only.
- **No breaking changes** to existing `spec <id>` behavior.
