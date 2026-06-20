## Context

`FoundrySpecGeneratorAgent.GenerateAsync` today runs three sequential C# steps
before touching the agent:

1. `FetchWorkItemAsync` → calls `GET /workitem/{id}` on the Tools API.
2. `QueryAzureSearchAsync(title)` → fires a single hybrid search against Azure AI
   Search using only the work item **title** as the query (not the description or
   AC). Returns top 5 snippets, each capped at 2 000 chars.
3. `BuildPayload` → assembles `{ workItem, projectConfig, devRules, repoContext }`
   and hands it to `_agentClient.RunAsync` as a single string.

The agent then produces the spec in **one completion**. It has no tools; the
`repoContext` it receives is all it will ever know about the codebase.

The Tools API (`Backlog2SpecAgent.Tools/Program.cs`) is a minimal API already
deployed on App Service. It exposes `GET /workitem/{id}`,
`GET /workitem/{id}/hierarchy`, `POST /repo-context`, `POST /spec`. It reads the
ADO repo via the Git Items API (Basic auth + PAT). It has no OpenAPI document
and no fine-grained navigation endpoints.

`FoundryAgentClient.RunAsync` creates a thread, posts the message, polls until
the run reaches a terminal state, and returns the last assistant text. It already
handles multi-step runs in which the agent performs tool calls before responding —
this is the Foundry Agent Service's native behaviour when an OpenAPI tool is
registered.

## Goals / Non-Goals

**Goals:**
- Give the agent read-time access to the codebase via navigation tools so it can
  investigate before committing to a file list.
- Make `filesToChange` entries verifiably grounded (evidence + confidence).
- Remove the fragile C#-side Azure AI Search retrieval; replace it with
  agent-driven retrieval using the same Tools API over HTTP.
- Keep `FoundryAgentClient`, `IFoundryAgentClient`, and the CLI surface
  (`SpecCommand`, `--mock`, `--raw`, `--feature`, `--epic`) completely unchanged.

**Non-Goals:**
- Rewriting `FoundryAgentClient` or the polling loop.
- Adding code-symbol intelligence (LSP, tree-sitter, Roslyn) — regex-based
  reference search is acceptable as a first iteration.
- Changing how the spec is written to disk or consumed by callers.
- Replacing the agent on the Foundry portal — only the system prompt, tool
  registration, and optionally the model deployment change.

## Decisions

### D1: Register the Tools API as an OpenAPI tool rather than native Foundry functions

Options considered:
- **Native Foundry function definitions (JSON schema)** — requires duplicating the
  endpoint contract in JSON inside the Foundry portal. Fragile: any endpoint
  change must be reflected manually.
- **OpenAPI tool (chosen)** — import the `/swagger/v1/swagger.json` produced by
  the Tools API itself. Single source of truth. Foundry auto-generates the tool
  schema from the document. API changes are reflected on the next import.
- **Azure Functions with Foundry tool bindings** — over-engineered; the Tools API
  is already an App Service.

### D2: Add Swashbuckle to the Tools API (not a separate spec file)

The OpenAPI document must be generated from the code, not hand-authored, to stay
in sync. `Swashbuckle.AspNetCore` integrates with ASP.NET Core minimal APIs via
`AddEndpointsApiExplorer` + `AddSwaggerGen`. Each endpoint gets `operationId`,
`summary`, and `description` via `.WithOpenApi(op => { op.OperationId = ...; })`.

The document is served at `/swagger/v1/swagger.json` (Foundry import URL).
The `/swagger` UI path is optional but included for manual testing.

### D3: Feature-flag the C#-side Azure AI Search, don't delete it

Removing the Azure Search call immediately is the clean path, but the Search
index took effort to build and may be useful as a fallback. Add a config key
`AzureSearch:UseClientSideRetrieval` (default `false`). When `false`,
`FoundrySpecGeneratorAgent` sends `{ workItem, projectConfig, devRules }` with no
`repoContext`. When `true`, the old pre-fetch path runs (useful during rollout).

This lets the team A/B-test the new agent behaviour without redeploy.

### D4: Evidence is a free-text string, not a structured reference

Options considered:
- **Structured citation** `{ file, line, snippet }` — richer but makes the JSON
  schema more complex and harder for the model to fill reliably.
- **Free-text string (chosen)** — e.g. `"readFile /src/Foo.cs:42-60 shows that
  HandleX calls RepositoryY.Save"`. Simpler schema; the model fills it naturally.
  The key invariant is that the string must be non-empty for every entry in
  `filesToChange`.

### D5: `listDirectory` uses `recursionLevel=oneLevel`, not `full`

`recursionLevel=full` on a large repo can return thousands of entries and is
already used by `POST /repo-context`. The new `GET /repo/tree` uses `oneLevel`
so the agent can navigate incrementally (mirrors how a developer browses the
explorer), keeping response sizes manageable per call.

### D6: `readFile` line-range cap at 400 lines, no content truncation

The existing `FetchFileContentAsync` caps at 2 000 chars — too short for the
agent to read a real class. `GET /repo/file` returns the requested line range
(max 400 lines) without character truncation. The agent uses `getFileOutline`
first to orient itself, then `readFile` to read the specific area of interest.

### D7: `findReferences` is regex whole-word, case-sensitive, capped at 50 results

A language-server or Roslyn-based implementation would be more precise but
requires significant infrastructure. Regex whole-word search across source files
is sufficient for finding callers, interface implementations, and usages.
The cap of 50 results keeps response sizes bounded.

## Schema Changes

### `GeneratedSpec` (and matching `FoundrySpec` internal class)

Before:
```json
{
  "goal": "string",
  "behaviour": ["string"],
  "edgeCases": ["string"],
  "outOfScope": ["string"],
  "filesToChange": ["string"]
}
```

After:
```json
{
  "goal": "string",
  "behaviour": ["string"],
  "edgeCases": ["string"],
  "outOfScope": ["string"],
  "filesToChange": [
    { "file": "string", "change": "string",
      "evidence": "string", "confidence": "high|medium|low" }
  ],
  "openQuestions": ["string"],
  "conventions": ["string"]
}
```

`OutputRenderer.RenderSpec` renders confidence as a colour badge (green/yellow/
red) using Spectre.Console markup. `--raw` outputs the full JSON as-is.

## New Endpoint Contracts

```
GET  /repo/tree?path=/src&dir=...        → [{ path, type }]
GET  /repo/file?path=/src/Foo.cs&startLine=10&endLine=50
                                         → { path, startLine, endLine, totalLines, content }
POST /repo/references { "symbol": "..." } → [{ path, line, snippet }]
GET  /repo/outline?path=/src/Foo.cs     → { path, symbols: [{ kind, name, signature, line }] }
```

All new endpoints are protected by the existing `X-Api-Key` middleware.
`operationId` values: `listDirectory`, `readFile`, `findReferences`,
`getFileOutline`. Existing endpoints get `operationId` values: `getWorkItem`,
`getWorkItemHierarchy`, `searchCode`, `saveSpec`.

## Risks / Trade-offs

- **Latency increase**: the agent now performs 3–8 tool calls before responding.
  For a typical PBI expect 10–25 seconds additional latency. Mitigate by setting
  a tool-call cap in the system prompt and choosing a fast reasoning model.
- **Cost increase**: more tokens per run (tool responses in context). Mitigate
  with `AzureSearch:UseClientSideRetrieval = true` as a fast/cheap fallback for
  simple tickets.
- **ADO API rate limits**: `findReferences` fetches many files sequentially.
  It caps at 50 results and stops early; acceptable for a single-user CLI tool.
- **Regex reference search misses**: renames, interface indirection, and generic
  usages will not be found. This is a known limitation of the first iteration;
  uncertain results go to `openQuestions`, not to false `filesToChange` entries.

## Open Questions

- Should `getFileOutline` for non-.cs files fall back to a line count + extension
  rather than returning an empty symbol list? (Current decision: return empty
  symbols with a note; the agent can still `readFile` the whole thing.)
- What is the right tool-call cap for the system prompt? Suggested starting point:
  12 tool calls. Tune based on tracing data after rollout.
- Should confidence `"low"` entries be excluded from `filesToChange` and moved to
  `openQuestions` automatically? (Current decision: include them, let the
  developer decide; the confidence badge makes them visible.)
