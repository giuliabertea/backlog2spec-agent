## Context

`WikiContextAgent` was introduced to enrich ticket analysis with project knowledge, but wiki pages are often stale or absent. The ADO Git REST API exposes the same repository content developers write and read daily. This design replaces the wiki retrieval path with direct source file retrieval using the same PAT already in user-secrets.

Current pipeline step 3:
```
WikiContextAgent → IReadOnlyList<WikiPageDto> → EnrichmentAgent
```

Target:
```
CodebaseContextAgent → IReadOnlyList<CodeFileDto> → EnrichmentAgent
```

All other pipeline steps (AdoClient, EnrichmentAgent, SpecGeneratorAgent, OutputRenderer) remain unchanged except for the type substitution.

## Goals / Non-Goals

**Goals:**
- Replace wiki retrieval with ADO Git file retrieval using the same HTTP + Basic auth pattern
- Keep the feature opt-in (`ado.repoName` absent = skip, no error)
- Provide the LLM with actual source snippets (class declarations, interface definitions) rather than prose docs
- Degrade gracefully on any API error without failing the pipeline

**Non-Goals:**
- Semantic/embedding-based code search (out of scope for Phase 1)
- Fetching binary files, packages, or generated output
- Multi-repo support (single repo per config)
- Caching retrieved files between runs

## Decisions

### D1 — ADO Git Items API over cloning or embedding

**Decision:** Use `GET /{org}/{project}/_apis/git/repositories/{repo}/items` with `recursionLevel=full` to list all file paths, then fetch individual files by path.

**Rationale:** No extra dependencies, same auth header as `WikiContextAgent`. Cloning the repo is too heavy for a CLI tool. Embedding search requires Azure AI Search infrastructure which belongs to Phase 2.

**Alternatives considered:**
- `git clone --depth=1`: Too slow and requires git on PATH; inappropriate for a CLI tool that should work anywhere.
- Azure AI Search vector index: Correct long-term solution but out of scope for Phase 1 demo.

### D2 — Keyword scoring on file paths (not content)

**Decision:** Extract keywords from the ticket title, score each file path by how many keywords appear as substrings, fetch content only for the top 3.

**Rationale:** Fetching content for all files would be too slow and expensive. Path-based scoring is fast, deterministic, and works well in practice because ADO repos use feature-based folder structures (`Profitability/`, `Snapshot/`, `Booking/`).

**Alternatives considered:**
- Scoring on file content: Requires fetching everything first — O(n) API calls before any selection.
- First N files alphabetically: No relevance signal at all.

### D3 — Reuse `IEnrichmentAgent` parameter slot, change type only

**Decision:** Keep the three-parameter signature of `EnrichAsync`. Rename the third parameter from `wikiContext` to `codebaseContext` and change its type from `IReadOnlyList<WikiPageDto>` to `IReadOnlyList<CodeFileDto>`.

**Rationale:** Minimal blast radius. `SpecCommand` already calls this parameter slot; only the DI registration and the concrete type change. Mock implementations need one-line updates.

### D4 — `CodeFileDto` mirrors `WikiPageDto` structure

**Decision:** `CodeFileDto` has the same three fields as `WikiPageDto`: `Path` (string), `Content` (string, capped at 800 chars), and a display-friendly `FileName` (the last segment of the path, e.g. `ProfitabilityService.cs`).

**Rationale:** The enrichment prompt template already handles a list of titled snippets. Only the section header and field names in `BuildPrompt` need to change.

### D5 — Filter non-source files before scoring

**Decision:** Skip files whose extension is not in a whitelist: `.cs`, `.ts`, `.js`, `.py`, `.java`, `.go`, `.md`.

**Rationale:** Avoids sending binary content or lock files to the LLM. `.md` files are included because they often contain README context at feature folder level.

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| Repo has thousands of files — listing tree is slow | ADO returns the full tree in a single paginated call; cap scoring to the first 2000 paths if needed |
| File content contains secrets (tokens, connection strings) | Content is truncated to 800 chars — enough for class/interface signatures, not enough to expose full secret blocks; same risk exists with wiki pages |
| Keyword matching misses relevant files when names don't reflect domain | Acceptable for Phase 1; Phase 2 (vector search) solves this properly |
| PAT lacks `Code: Read` scope | Error caught in the `try/catch`, logged as warning, pipeline continues with empty context |

## Migration Plan

1. Delete wiki files (4 files)
2. Add codebase files (4 files)
3. Update config model (`AdoConfig`: remove `WikiName`, add `RepoName?` + `Branch?`)
4. Update `IEnrichmentAgent` signature and all implementations
5. Update `SpecCommand` DI injection
6. Update `Program.cs` registrations
7. Update prompt template
8. Update `backlog-2-spec.json` example
9. Build and run `--mock` to verify pipeline

No database migrations, no deployment steps. CLI tool only.

## Open Questions

- Should `branch` support full ref syntax (`refs/heads/main`) or short names only (`main`, `master`)? Short names are simpler and cover all real use cases — use short names, let the API resolve them.
- Should `.md` files be included in the extension whitelist? Yes — feature-level READMEs often contain domain context useful to the LLM.
