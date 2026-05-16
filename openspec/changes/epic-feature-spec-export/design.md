## Context

The current pipeline handles a single PBI: fetch one work item → enrich → generate → render to console. Adding hierarchy support means fetching a parent (Feature or Epic) plus its direct children, generating a spec per child, and writing all output to the file system rather than the console.

The ADO SDK already returns work item **relations** when `expand: WorkItemExpand.Relations` is used. A child work item appears as a relation with `rel == "System.LinkTypes.Hierarchy-Forward"`. IDs can be parsed from the relation URL (`_apis/wit/workItems/{id}`). This avoids WIQL queries entirely.

Relevant existing classes: `AdoClient`, `IAdoClient`, `WorkItemDto`, `SpecCommand`, `IOutputRenderer`, `OutputRenderer`.

## Goals / Non-Goals

**Goals:**
- Fetch a Feature or Epic and all its **direct** child work items in one ADO call batch.
- Generate a summary file for the parent and one spec file per child, written to `spec/<id>-<slug>/`.
- Keep existing `spec <id>` behavior completely unchanged.
- Reuse the full enrich → generate pipeline for each child.

**Non-Goals:**
- Recursive multi-level traversal (Epic → Feature → PBI → Task). Direct children only.
- Auto-detecting work item type from the ID (explicit flags only, for clarity).
- Streaming or parallel spec generation per child (sequential is fine for now).
- New command (`feature`/`epic` subcommand); flags on the existing `spec` command suffice.

## Decisions

### 1. Flags: `--feature` and `--epic` on the existing `spec` command
**Alternatives considered:** A separate `hierarchy` subcommand; auto-detect from work item type.  
**Decision:** Add `--feature` and `--epic` as optional flags on `spec`. They are mutually exclusive and simply tell `SpecCommand` to enter hierarchy mode. The existing `<id>` argument is reused. This keeps the CLI surface minimal and avoids a breaking change.

### 2. ADO child resolution via relations, not WIQL
**Alternatives considered:** WIQL query `SELECT [Id] FROM WorkItemLinks WHERE Source=[id] AND LinkType = 'Child'`.  
**Decision:** Fetch the parent with `WorkItemExpand.Relations`, filter for `System.LinkTypes.Hierarchy-Forward`, parse child IDs from relation URLs, then batch-fetch children with `GetWorkItemsAsync(ids, expand: WorkItemExpand.All)`. This is one network round-trip for the parent and one batch call for all children, vs. one WIQL call + N individual fetches.

### 3. New `WorkItemHierarchyDto` model
A thin wrapper: `WorkItemDto Parent` + `IReadOnlyList<WorkItemDto> Children`. Keeps the existing `WorkItemDto` unchanged and avoids polluting it with hierarchy concerns.

### 4. New `GetWorkItemHierarchyAsync` method on `IAdoClient`
Added to the existing interface rather than a new class. Rationale: the ADO client is already the seam for all ADO I/O; a second class would require its own auth/connection setup duplicating that logic.

### 5. `IOutputRenderer.WriteHierarchyToFiles` for file output
**Alternatives considered:** A separate `SpecFileWriter` service.  
**Decision:** Extend `IOutputRenderer` with `WriteHierarchyToFiles(WorkItemDto parent, IEnumerable<(WorkItemDto item, GeneratedSpec spec)> children, string outputDir)`. `OutputRenderer` already owns all output concerns; file-writing is the appropriate extension of that contract. The console-only path is untouched.

### 6. Output folder: `spec/<id>-<slug>/` relative to CWD
Slug is the parent title lowercased, non-alphanumeric chars replaced with hyphens, max 60 chars. Using the ID prefix ensures uniqueness even when titles change or collide.

## Risks / Trade-offs

- **Large child counts** → many sequential AI calls, slow UX. Mitigation: show a progress step per child ("Generating spec for #1234 (2/8)..."). Sequential generation keeps token budget predictable.
- **Partial failure** (one child fails mid-run) → some files written, some not. Mitigation: on any child error, log a warning, skip that child, and continue. Report skipped items at the end.
- **Relations API may return non-child links** (e.g., Related, Predecessor). Mitigation: filter strictly on `System.LinkTypes.Hierarchy-Forward` relation type.
- **`MockAdoClient` needs updating** for tests to exercise the new interface method. Low risk — it just needs to return a hard-coded `WorkItemHierarchyDto`.

## Migration Plan

No data migration needed. The change is purely additive:
1. Extend `IAdoClient` and `AdoClient` with the hierarchy method.
2. Add `WorkItemHierarchyDto` model.
3. Extend `IOutputRenderer` / `OutputRenderer` with `WriteHierarchyToFiles`.
4. Add flags and hierarchy branch in `SpecCommand`.
5. Update `MockAdoClient` to implement the new interface method.
6. Existing `spec <id>` path is untouched; existing tests continue to pass.

## Open Questions

- Should `--feature` and `--epic` be collapsed into a single `--hierarchy` flag (treating them identically)? The work item type is already in `WorkItemDto.WorkItemType`, so validation could happen at fetch time rather than needing separate flags.
- Should the summary file (`_feature.md` / `_epic.md`) include a rendered spec of the parent itself, or only metadata + child index?
