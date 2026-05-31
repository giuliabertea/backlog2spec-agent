# Backlog2SpecAgent — Codebase Guide

> For developers new to this project. Everything below is derived from the actual source code.

---

## 1. Project Overview

**What it does:** Backlog2SpecAgent is a .NET 8 CLI tool that turns an Azure DevOps (ADO) work item into a structured, AI-enriched implementation spec — in seconds. You give it a work item ID; it pulls the ticket, runs it through an LLM pipeline that identifies gaps and generates a spec, then renders the result to the terminal or a markdown file.

**Problem it solves:** ADO tickets are often incomplete (missing edge cases, acceptance criteria, affected files). Developers waste time figuring out what to build before they can start. Backlog2SpecAgent automates that analysis step.

**Tech stack:**

| Technology | Version | Role |
|---|---|---|
| .NET / C# | 8.0 | Runtime and language |
| System.CommandLine | 2.0.0-beta4 | CLI argument parsing |
| Microsoft.SemanticKernel | 1.75.0 | LLM orchestration |
| Microsoft.TeamFoundationServer.Client | 20.256.2 | Azure DevOps API client |
| Spectre.Console | 0.55.2 | Rich terminal output |
| HtmlAgilityPack | 1.12.4 | Strip HTML from ADO descriptions |
| Microsoft.Extensions.Hosting | 10.0.7 | Dependency injection |
| xUnit | 2.5.3 | Test framework |

**Architecture style:** Layered CLI application — command handler → service layer → AI agents → ADO integration. Each layer is decoupled through interfaces to support dependency injection and mock implementations.

---

## 2. Project Structure

```
Backlog2SpecAgent/
│
├── src/
│   └── Backlog2SpecAgent.Cli/              ← The entire application lives here
│       ├── Ado/                       ← Azure DevOps client (fetch work items + repo files)
│       ├── Agents/                    ← AI enrichment and spec generation logic
│       ├── Commands/                  ← CLI command definitions and orchestration
│       ├── Config/                    ← Config file loading and validation
│       ├── Kernel/                    ← Semantic Kernel (LLM) factory setup
│       ├── Models/                    ← DTOs and data models
│       ├── Output/                    ← Console rendering and file writing
│       ├── Prompts/                   ← AI prompt templates (plain text files)
│       ├── Services/                  ← (Currently minimal, reserved for future logic)
│       └── Program.cs                 ← Entry point: DI wiring + CLI bootstrap
│
├── tests/
│   └── Backlog2SpecAgent.Tests/            ← xUnit test project (mostly placeholder tests)
│
├── openspec/
│   ├── specs/                         ← Active OpenSpec specifications for this project
│   └── changes/                       ← Archived design/task artifacts per feature
│
├── spec/                              ← Output folder: generated spec files land here
├── .config/dotnet-tools.json          ← dotnet tool manifest
├── .claude/                           ← Claude Code integration settings
├── Backlog2SpecAgent.sln                   ← Visual Studio solution file
├── backlog-2-spec.json                ← Example config (template for users)
└── README.md                          ← Setup and usage guide
```

**Most important folders to understand first (in order):**
1. `Commands/` — the orchestration hub
2. `Agents/` — the AI pipeline
3. `Ado/` — the data source
4. `Config/` — what drives runtime behavior
5. `Output/` — what the user actually sees

---

## 3. Entry Points

### How the app starts

`src/Backlog2SpecAgent.Cli/Program.cs` is the entry point.

It does four things in sequence:

1. **Parses `--mock` early** — before DI is configured, so it can skip loading secrets
2. **Loads secrets** — Azure AI credentials and ADO PAT from the .NET User Secrets store
3. **Builds the DI container** — registers all services (real or mock depending on the flag)
4. **Invokes the CLI** — creates `SpecCommand`, calls `.Invoke(args)`

```
dotnet backlog-2-spec spec 12345
         ↓
    Program.cs
         ↓
    SpecCommand registered as root command
         ↓
    System.CommandLine parses args → calls SetHandler()
         ↓
    ExecuteAsync() / ExecuteHierarchyAsync()
```

### How it is configured and launched

There are two separate configuration sources:

**1. User secrets (credentials — never committed to repo):**

Direct mode:
```
AzureAI:Endpoint        → LLM service URL
AzureAI:ApiKey          → LLM API key
AzureAI:DeploymentName  → e.g. "gpt-4o"
AzureAI:EndpointType    → "AzureOpenAI" or "AzureFoundry"
Ado:Pat                 → Azure DevOps Personal Access Token
```

Agent mode (add these):
```
AzureAI:UseAgent        → "true"
AzureAI:ProjectEndpoint → Azure OpenAI base URL (https://<resource>.openai.azure.com/openai)
AzureAI:AgentId         → Assistant ID from Azure OpenAI Studio
AzureAI:ToolsBaseUrl    → HTTP endpoint of the Tools API
AzureAI:ToolsApiKey     → Shared secret for the Tools API
AzureSearch:Endpoint    → Azure AI Search service URL (https://<name>.search.windows.net)
AzureSearch:ApiKey      → Azure AI Search admin key
AzureSearch:IndexName   → Index name (default: "codebase-chunks")
```
Set via: `dotnet user-secrets set "AzureAI:Endpoint" "https://..."`

**2. Project config file (`backlog-2-spec.json`):**
Searched upward from the current working directory. Contains project metadata and ADO coordinates. See [Config/ConfigLoader.cs](src/Backlog2SpecAgent.Cli/Config/ConfigLoader.cs).

---

## 4. Module Breakdown

### `Commands/` — Orchestration

**Key file:** [SpecCommand.cs](src/Backlog2SpecAgent.Cli/Commands/SpecCommand.cs)

This is the nerve center of the application. It defines the `spec` command, its arguments (`<id>`), and options (`--verbose`, `--raw`, `--mock`, `--output`, `--feature`, `--epic`). Its `SetHandler()` method calls either:

- `ExecuteAsync()` — single work item pipeline
- `ExecuteHierarchyAsync()` — feature or epic (fetches parent + all children, generates one spec per child, writes all to a folder)

Error handling is also centralized here: custom exceptions (`ConfigException`, `AdoNotFoundException`, `AdoAuthException`, `LlmFormatException`) are caught and translated to user-friendly console messages.

**Communicates with:** all other modules via DI-injected interfaces.

---

### `Ado/` — Azure DevOps Integration

**Key files:**
- [IAdoClient.cs](src/Backlog2SpecAgent.Cli/Ado/IAdoClient.cs) — interface
- [AdoClient.cs](src/Backlog2SpecAgent.Cli/Ado/AdoClient.cs) — real implementation
- [MockAdoClient.cs](src/Backlog2SpecAgent.Cli/Ado/MockAdoClient.cs) — hardcoded mock

**What it does:**
- `GetWorkItemAsync(int id)` — fetches a single work item with all fields + relations
- `GetWorkItemHierarchyAsync(int parentId)` — fetches parent + all child work items

**Authentication:** PAT (Personal Access Token) passed to `VssConnection`.

**HTML stripping:** ADO stores descriptions as HTML. `HtmlAgilityPack` is used to convert to plain text before passing to the AI.

**Error mapping:**
- "does not exist" / "TF401232" / "VS403417" → `AdoNotFoundException`
- "unauthorized" / "Access Denied" → `AdoAuthException`

**ADO REST API** (used in `CodebaseContextAgent`, not `AdoClient` itself):
- List repo files: `_apis/git/repositories/{repoName}/items?recursionLevel=full`
- Fetch file content: `_apis/git/repositories/{repoName}/items?path=...&includeContent=true`

---

### `Agents/` — AI Pipeline

This is where the two-step AI enrichment happens. Both agents follow the same pattern:
load prompt template → fill variables → call LLM → parse JSON → retry up to 2 times on failure.

**`FoundrySpecGeneratorAgent.cs`** — Agent-mode spec generation
- Used when `AzureAI:UseAgent = true` (replaces the two-step `EnrichmentAgent` + `SpecGeneratorAgent` pipeline)
- Calls the Tools API `GET /workitem/{id}` (`X-Api-Key` auth) to fetch the work item
- Queries Azure AI Search directly: `POST {searchEndpoint}/indexes/{indexName}/docs/search` with the work item title as the query; returns up to 5 `{ filePath, content }` chunks
- Builds a single JSON payload `{ workItem, projectConfig, repoContext: [{ file, content }] }` and sends it to `FoundryAgentClient.RunAsync()`
- Search failures are non-fatal: the pipeline continues with `repoContext: null`
- Parses the JSON response into `GeneratedSpec`, retrying up to 2 times on JSON errors

**`CodebaseContextAgent.cs`** — Codebase file fetcher
- Only runs when `repoName` is set in config (Direct mode only — not used in Agent mode)
- Lists all files in the ADO repo, scores them by keyword match, fetches the top candidates, re-scores by content, returns the top 8 files (≤ 2000 chars each)
- If this fails for any reason, it logs a warning and returns an empty list — the pipeline continues without context

**`EnrichmentAgent.cs`** — Gap analysis via LLM
- Sends the work item to the LLM with the enrichment prompt
- Receives a JSON `EnrichedTicket` with: `missingAcceptanceCriteria`, `edgeCases`, `constraints`, `affectedComponents`, `ambiguities`
- Temperature = 0.1 (near-deterministic)

**`SpecGeneratorAgent.cs`** — Spec synthesis via LLM (Direct mode)
- Takes the `EnrichedTicket` + codebase context
- Sends to LLM with the spec prompt
- Receives a JSON `GeneratedSpec` with: `goal`, `behaviour`, `edgeCases`, `outOfScope`, `filesToChange`
- Temperature = 0.1

**`LlmKeywordExtractor.cs` / `StopwordKeywordExtractor.cs`** — Keyword extraction
- Used by `CodebaseContextAgent` to extract search terms from the work item title and description
- `LlmKeywordExtractor` asks the LLM for 5–8 semantic keywords; falls back to `StopwordKeywordExtractor` on error
- `StopwordKeywordExtractor` splits text, filters short words and common stopwords

**Mock agents:** `MockEnrichmentAgent`, `MockSpecGeneratorAgent`, `MockCodebaseContextAgent` return hardcoded data and are used when `--mock` is passed.

---

### `Config/` — Configuration Loading

**Key files:**
- [ConfigLoader.cs](src/Backlog2SpecAgent.Cli/Config/ConfigLoader.cs)
- [AgentConfig.cs](src/Backlog2SpecAgent.Cli/Config/AgentConfig.cs)

`ConfigLoader.LoadAsync()` walks up the directory tree from CWD looking for `backlog-2-spec.json`. It deserializes it into `AgentConfig` and validates required fields. If `devRulesFile` is specified, it reads that file's content and injects it into `AgentConfig.DevRulesContent`.

**Config shape:**
```json
{
  "project": { "name": "", "language": "", "framework": "", "testFramework": "", "architecture": "" },
  "conventions": { "naming": "", "folderStructure": "", "specStyle": "", "diPattern": "" },
  "ado": { "organization": "", "project": "", "repoName": "", "branch": "" },
  "devRulesFile": "path/to/rules.md"
}
```

`AgentConfig` is the single config object passed through the entire pipeline. It is immutable (`init` properties).

---

### `Kernel/` — LLM Setup

**Key file:** [KernelFactory.cs](src/Backlog2SpecAgent.Cli/Kernel/KernelFactory.cs)

Creates and returns a `Microsoft.SemanticKernel.Kernel` instance. Supports two Azure AI endpoint styles:

- **AzureOpenAI** (classic): `https://<resource>.openai.azure.com` — uses `AddAzureOpenAIChatCompletion()`
- **AzureFoundry** (recommended): `https://<name>.<region>.inference.ai.azure.com` — uses `AddOpenAIChatCompletion()` with a custom base URL

The kernel is injected into agents via DI and used to invoke chat completions.

---

### `Models/` — Data Transfer Objects

All models use C# `sealed class` or `record` with `init`-only properties (immutable).

| Type | Used for |
|---|---|
| `WorkItemDto` | ADO work item data (id, title, type, description, AC) |
| `WorkItemHierarchyDto` | Parent + list of children |
| `CodeFileDto` | A source file fetched from the repo (path, filename, content) |
| `EnrichedTicket` | AI enrichment output |
| `GeneratedSpec` | AI spec generation output |

---

### `Output/` — Rendering

**Key file:** [OutputRenderer.cs](src/Backlog2SpecAgent.Cli/Output/OutputRenderer.cs)

Uses `Spectre.Console` for formatted terminal output. Key methods:

- `RenderSpec()` — renders the final spec to the console with colored sections
- `RenderVerboseDetail()` — shows enrichment output (only with `--verbose`)
- `RenderMarkdown()` — writes a single spec as a `.md` file with metadata header
- `WriteHierarchyToFiles()` — for `--feature`/`--epic`: creates `spec/<id>-<slug>/`, writes `_summary.md` + one file per child
- `RenderRaw()` — dumps JSON to console (for `--raw`)
- `Slugify()` — converts a title to a URL-safe slug (max 60 chars)

---

### `Prompts/` — AI Prompt Templates

Two plain-text files. They use a custom variable syntax (`{{VariableName}}`):

- [enrichment.txt](src/Backlog2SpecAgent.Cli/Prompts/enrichment.txt) — instructs the LLM to identify gaps in the work item
- [spec.txt](src/Backlog2SpecAgent.Cli/Prompts/spec.txt) — instructs the LLM to generate the implementation spec

Both prompts are embedded in the assembly as resources and loaded at runtime by their respective agents. The agents fill in template variables (project name, language, dev rules, codebase context, work item fields) before sending to the LLM.

---

## 5. Data Flow

### Single Work Item (end-to-end)

```
User: dotnet backlog-2-spec spec 12345
       │
       ▼
  Program.cs
  ├── Parse --mock flag
  ├── Load user secrets (Azure AI + ADO PAT)
  ├── Build DI container (real or mock services)
  └── Invoke SpecCommand
       │
       ▼
  SpecCommand.ExecuteAsync()
  ├── ConfigLoader.LoadAsync()
  │     └── Reads backlog-2-spec.json → AgentConfig
  │
  ├── AdoClient.GetWorkItemAsync(12345)
  │     └── VssConnection → ADO API → WorkItemDto
  │         (HTML stripped via HtmlAgilityPack)
  │
  ├── CodebaseContextAgent.FetchRelevantFilesAsync(workItem, config)
  │     ├── LlmKeywordExtractor → keywords from title/description
  │     ├── ADO REST API → list all repo files
  │     ├── Score paths by keyword matches
  │     ├── ADO REST API → fetch top 40 file contents (parallel)
  │     ├── Score contents by keyword matches
  │     └── Returns top 8 CodeFileDto[]
  │
  ├── EnrichmentAgent.EnrichAsync(workItem, config, codebaseContext)
  │     ├── Load enrichment.txt prompt template
  │     ├── Fill variables (project info, dev rules, codebase context, work item)
  │     ├── SemanticKernel → LLM call (temp=0.1)
  │     ├── Extract JSON from response
  │     └── Deserialize → EnrichedTicket
  │         (retry up to 2x on JSON parse failure)
  │
  ├── SpecGeneratorAgent.GenerateAsync(enrichedTicket, config, codebaseContext)
  │     ├── Load spec.txt prompt template
  │     ├── Fill variables (project info, dev rules, enriched ticket, codebase context)
  │     ├── SemanticKernel → LLM call (temp=0.1)
  │     ├── Extract JSON from response
  │     └── Deserialize → GeneratedSpec
  │         (retry up to 2x on JSON parse failure)
  │
  └── OutputRenderer
        ├── RenderSpec() → styled terminal output
        ├── RenderVerboseDetail() → (if --verbose)
        ├── RenderMarkdown() → (if --output <path>)
        └── RenderRaw() → (if --raw)
```

### Agent Mode — Single Work Item (`AzureAI:UseAgent = true`)

```
SpecCommand.ExecuteAsync()
├── ConfigLoader.LoadAsync()
│
└── FoundrySpecGeneratorAgent.GenerateAsync(workItemId)
      ├── GET {ToolsBaseUrl}/workitem/{id}  (X-Api-Key header) → workItemJson
      ├── POST {SearchEndpoint}/indexes/{IndexName}/docs/search  (api-key header)
      │     body: { search: <title>, select: "filePath,content", top: 5 }
      │     → repoContext: [{ file, content }]  (empty list on failure — non-fatal)
      ├── Build payload: { workItem, projectConfig, repoContext }
      ├── FoundryAgentClient.RunAsync(payload)
      │     ├── POST {ProjectEndpoint}/threads           → threadId
      │     ├── POST /threads/{threadId}/messages        → (user message added)
      │     ├── POST /threads/{threadId}/runs            → runId
      │     ├── GET  /threads/{threadId}/runs/{runId}    (poll every 2s until completed/failed)
      │     ├── GET  /threads/{threadId}/messages        → first assistant message text
      │     └── DELETE /threads/{threadId}               (cleanup)
      │         (all calls use api-key header)
      └── Parse JSON → GeneratedSpec
          (retry up to 2x on JSON parse failure)
```

### Feature/Epic Export (hierarchy)

```
SpecCommand.ExecuteHierarchyAsync()
├── AdoClient.GetWorkItemHierarchyAsync(id)
│     └── WorkItemHierarchyDto { Parent, Children[] }
│
├── For each child:
│     ├── CodebaseContextAgent (same as above)
│     ├── EnrichmentAgent (same as above)
│     ├── SpecGeneratorAgent (same as above)
│     └── Collect GeneratedSpec (errors are caught, child is skipped)
│
└── OutputRenderer.WriteHierarchyToFiles()
      ├── Creates folder: spec/<parentId>-<slug>/
      ├── Writes spec/<parentId>-<slug>/_summary.md (index with links)
      └── Writes spec/<parentId>-<slug>/<childId>-<slug>.md per child
```

---

## 6. Key Abstractions & Patterns

### Interfaces + Mock pattern

Every external dependency is behind an interface with a real and a mock implementation:

| Interface | Real (Direct mode) | Real (Agent mode) | Mock |
|---|---|---|---|
| `IAdoClient` | `AdoClient` | `AdoClient` | `MockAdoClient` |
| `ISpecGeneratorAgent` | `SpecGeneratorAgent` | `FoundrySpecGeneratorAgent` | `MockSpecGeneratorAgent` |
| `IFoundryAgentClient` | — | `FoundryAgentClient` | `MockFoundryAgentClient` |
| `IEnrichmentAgent` | `EnrichmentAgent` | — | `MockEnrichmentAgent` |
| `ICodebaseContextAgent` | `CodebaseContextAgent` | — | `MockCodebaseContextAgent` |
| `IKeywordExtractor` | `LlmKeywordExtractor` | — | `StopwordKeywordExtractor` (also a fallback) |
| `IOutputRenderer` | `OutputRenderer` | `OutputRenderer` | — |

DI registration is done in `Program.cs` — real or mock branch based on `--mock` flag.

### Immutable models

All data models use `sealed class` / `record` with `init`-only setters. There is no mutation after construction. This makes the pipeline easy to reason about — data flows forward, never backwards.

### JSON extraction pattern

Both `EnrichmentAgent` and `SpecGeneratorAgent` do not trust the LLM to return clean JSON. They use a helper method that finds the first `{` and last `}` in the response and extracts only that substring before deserializing. This handles LLMs that add preamble text or markdown code fences.

### Retry pattern

Both agents retry up to 2 times (3 total attempts) on `JsonException`. If all attempts fail, they throw `LlmFormatException` which is caught in `SpecCommand` and shown as a user-friendly error.

### Prompt template variables

Prompts use `{{VariableName}}` placeholders replaced via `string.Replace()`. Variables are injected from `AgentConfig` and the current work item. Dev rules and codebase context are injected as optional blocks only when non-empty.

### Custom exception hierarchy

All domain errors are typed exceptions rather than generic `Exception`. This allows `SpecCommand` to catch them specifically and display targeted, actionable messages.

---

## 7. External Integrations

### Azure DevOps

- **SDK:** `Microsoft.TeamFoundation.WorkItemTracking.WebApi` (part of `Microsoft.TeamFoundationServer.Client`)
- **Auth:** PAT via `VssBasicCredential`, stored in user secrets as `Ado:Pat`
- **Connection:** `VssConnection` created in `AdoClient` constructor using `Ado:Organization` URL
- **REST API (direct HTTP):** Used in `CodebaseContextAgent` for git file listing and content — standard `HttpClient` with `Authorization: Basic <base64(pat)>` header

### Azure AI / LLM (Direct mode)

- **SDK:** `Microsoft.SemanticKernel`
- **Auth:** API key stored in user secrets as `AzureAI:ApiKey`
- **Two endpoint styles** (set via `AzureAI:EndpointType`):
  - `AzureOpenAI` → standard Azure OpenAI resource
  - `AzureFoundry` → Azure AI Foundry model-as-a-service endpoint
- **Model:** deployment name configured as `AzureAI:DeploymentName` (typically `gpt-4o`)
- **Settings:** temperature = 0.1, no streaming — each agent call is a single `GetChatMessageContentsAsync()` call

### Azure OpenAI Assistants API (Agent mode)

- **No SDK** — direct `HttpClient` calls to the classic Assistants REST API (`2024-05-01-preview`)
- **Auth:** `api-key` header using `AzureAI:ApiKey` (same key as Direct mode — no separate credential)
- **Base URL:** `AzureAI:ProjectEndpoint` — must be `https://<resource>.openai.azure.com/openai`
- **Flow:** create thread → add user message → create run → poll until completed/failed → read messages → delete thread
- **Response parsing:** traverses `data[].content[].text.value` from the messages list; returns the first assistant message
- **Tools API:** `FoundrySpecGeneratorAgent` calls `GET /workitem/{id}` on `AzureAI:ToolsBaseUrl` with `X-Api-Key` auth before invoking the assistant

### Azure AI Search (Agent mode)

- **No SDK** — direct `HttpClient` call to the Search REST API (`2023-11-01`)
- **Auth:** `api-key` header using `AzureSearch:ApiKey`
- **Endpoint:** `POST {AzureSearch:Endpoint}/indexes/{AzureSearch:IndexName}/docs/search`
- **Request body:** `{ "search": "<work item title>", "select": "filePath,content", "top": 5 }`
- **Response:** `value[]` array of `{ filePath, content }` objects — mapped to `repoContext` in the agent payload
- **Failure mode:** any HTTP error or parse failure is logged as a warning and the pipeline continues without repo context

### Spectre.Console

- Used exclusively in `OutputRenderer`
- Provides colored sections, bullet lists, progress indicators
- All console writes go through the renderer — nothing writes directly to `Console` outside it

---

## 8. How to Navigate the Codebase

### Suggested reading order

1. [README.md](README.md) — setup instructions and CLI usage examples
2. [Program.cs](src/Backlog2SpecAgent.Cli/Program.cs) — DI wiring, secret loading, CLI setup (~80 lines, very readable)
3. [Commands/SpecCommand.cs](src/Backlog2SpecAgent.Cli/Commands/SpecCommand.cs) — the full orchestration flow; read `ExecuteAsync()` first
4. [Config/AgentConfig.cs](src/Backlog2SpecAgent.Cli/Config/AgentConfig.cs) — the shape of all config data
5. [Models/WorkItemDto.cs](src/Backlog2SpecAgent.Cli/Models/WorkItemDto.cs) — what an ADO work item looks like in code
6. [Agents/EnrichmentAgent.cs](src/Backlog2SpecAgent.Cli/Agents/EnrichmentAgent.cs) — the first LLM call
7. [Prompts/enrichment.txt](src/Backlog2SpecAgent.Cli/Prompts/enrichment.txt) — what the enrichment LLM is actually told
8. [Agents/SpecGeneratorAgent.cs](src/Backlog2SpecAgent.Cli/Agents/SpecGeneratorAgent.cs) — the second LLM call
9. [Prompts/spec.txt](src/Backlog2SpecAgent.Cli/Prompts/spec.txt) — what the spec LLM is actually told
10. [Output/OutputRenderer.cs](src/Backlog2SpecAgent.Cli/Output/OutputRenderer.cs) — how results are rendered

### The 10 most important files

| File | Why read it first |
|---|---|
| [Program.cs](src/Backlog2SpecAgent.Cli/Program.cs) | Bootstraps everything; shows DI wiring and secret loading in one place |
| [Commands/SpecCommand.cs](src/Backlog2SpecAgent.Cli/Commands/SpecCommand.cs) | Full pipeline orchestration and all error handling |
| [Config/AgentConfig.cs](src/Backlog2SpecAgent.Cli/Config/AgentConfig.cs) | Defines every configurable field the app reads |
| [Config/ConfigLoader.cs](src/Backlog2SpecAgent.Cli/Config/ConfigLoader.cs) | Shows how config is discovered and validated at startup |
| [Ado/AdoClient.cs](src/Backlog2SpecAgent.Cli/Ado/AdoClient.cs) | All Azure DevOps integration in one file |
| [Agents/EnrichmentAgent.cs](src/Backlog2SpecAgent.Cli/Agents/EnrichmentAgent.cs) | First AI step; shows the retry pattern and JSON extraction |
| [Agents/SpecGeneratorAgent.cs](src/Backlog2SpecAgent.Cli/Agents/SpecGeneratorAgent.cs) | Second AI step; nearly identical pattern to enrichment |
| [Agents/CodebaseContextAgent.cs](src/Backlog2SpecAgent.Cli/Agents/CodebaseContextAgent.cs) | File scoring logic; most algorithmically complex file |
| [Prompts/spec.txt](src/Backlog2SpecAgent.Cli/Prompts/spec.txt) | Defines the output schema the entire tool produces |
| [Output/OutputRenderer.cs](src/Backlog2SpecAgent.Cli/Output/OutputRenderer.cs) | All rendering logic; shows Spectre.Console usage and file writing |

### Common gotchas

**`--mock` must come before argument parsing reads secrets.** The flag is pre-scanned with `args.Contains("--mock")` before System.CommandLine runs, because secret loading (including `AzureAI:ApiKey`, `AzureSearch:*`, etc.) happens during DI setup — before the command handler would normally read its options.

**Config is searched upward from CWD, not the binary location.** If you run the tool from the wrong directory and don't have a `backlog-2-spec.json` in the path, it will throw `ConfigException`. This surprises users who install the global tool and run it from an unrelated directory.

**Codebase context is entirely optional and fail-safe.** If `repoName` is not set, or if the repo API call fails for any reason, `CodebaseContextAgent` returns an empty list and the pipeline continues. The LLM will still generate a spec, but `filesToChange` entries may use the `ClassName?: description` format with `?` to indicate the path is unknown.

**The LLM is told to output exactly 5 JSON fields in a fixed order** (in `spec.txt`). This is intentional — the retry logic depends on being able to reliably extract and deserialize the response. Any prompt change that makes the LLM add extra fields or change field order will break deserialization.

**All tests are currently skipped.** Every test class has `[Fact(Skip = "Not implemented")]`. The mock implementations exist but the tests calling them are placeholders. Don't rely on `dotnet test` to catch regressions — the mock mode (`--mock`) is the primary sanity check.

**`--feature` and `--epic` are mutually exclusive** but use nearly identical code paths in `ExecuteHierarchyAsync()`. The difference is only in the ADO hierarchy fetch: both call `GetWorkItemHierarchyAsync()` — the flag only affects which label is shown in progress output and how the output folder is named.

**Dev rules file is read at config load time, not at agent call time.** If you change the dev rules file while the tool is running (unlikely but worth knowing), the change won't be picked up.

**`ContentMaxChars = 2000` per file.** Source files fetched from the repo are hard-truncated at 2000 characters. For large files this means the LLM sees only the top of the file. The scoring step mitigates this by preferring files where keywords appear early, but it's still a limitation.

<!-- @import "[TOC]" {cmd="toc" depthFrom=1 depthTo=6 orderedList=false} -->
