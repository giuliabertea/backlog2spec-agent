## Context

Backlog2SpecAgent Phase 1 is a greenfield .NET 8 global CLI tool (`backlog-2-spec`) with no prior codebase. The primary constraint is that this must be a production-quality, layered system from day one — not a prototype — because Phase 2 will extend it with `plan` and `scaffold` commands sharing the same core infrastructure. All architectural decisions here must hold under that future load.

Key constraints from the brief:
- Clean Architecture strictly enforced (no business logic in CLI layer)
- `OutputRenderer` is the sole class that writes to console
- `AdoClient` is the sole class that calls Azure DevOps
- No `.Result` or `.Wait()` — fully async
- LLM output must be valid JSON; retry up to 2 times before failing
- Secrets exclusively via `dotnet user-secrets`

## Goals / Non-Goals

**Goals:**
- Implement the full `backlog-2-spec spec <id>` pipeline end-to-end
- Enforce Clean Architecture boundaries via interface-driven dependency injection
- Produce a deterministic, structured `GeneratedSpec` from any valid ADO work item
- Package as a installable `dotnet tool` (`backlog-2-spec`, v0.1.0)
- Support `--verbose` and `--raw` flags; never crash on errors

**Non-Goals:**
- `plan` or `scaffold` commands (Phase 2)
- Writing results back to Azure DevOps (future phase)
- VS Code extension (future phase)
- Multi-tenant or cloud-hosted operation
- Support for non-Azure DevOps tracking systems

## Decisions

### 1. Layer separation enforced by project structure, not namespaces

**Decision**: Single project (`Backlog2SpecAgent.Cli`) with subdirectories enforcing layer roles by convention (Agents/, Ado/, Output/, etc.) rather than separate class library projects.

**Rationale**: For Phase 1 scope, a multi-project solution would add friction with no benefit — the interface boundaries already prevent layer leakage. Phase 2 can extract `Backlog2SpecAgent.Core` if the solution grows. Keeping one project speeds up bootstrap and tooling (user-secrets, dotnet tool packaging).

**Alternative considered**: Separate `Backlog2SpecAgent.Core` and `Backlog2SpecAgent.Cli` projects. Rejected — over-engineering for Phase 1; interfaces achieve the same decoupling contract.

---

### 2. Microsoft Semantic Kernel for LLM orchestration

**Decision**: Use `Microsoft.SemanticKernel` with Azure OpenAI for both the `EnrichmentAgent` and `SpecGeneratorAgent`.

**Rationale**: Semantic Kernel provides prompt templating, retry infrastructure, and a clean `IChatCompletionService` abstraction that will support future agent features (memory, plugins). Using it from Phase 1 avoids a painful migration in Phase 2.

**Alternative considered**: Direct `Azure.AI.OpenAI` SDK calls. Rejected — no retry, no prompt management, higher coupling to Azure-specific API shapes.

---

### 3. JSON-only LLM contract with schema-in-prompt enforcement

**Decision**: Prompt templates for both agents instruct the model to return only valid JSON matching an embedded schema. The agent parses the response with `System.Text.Json`, validates structure, and retries up to 2 times on parse failure.

**Rationale**: Markdown-wrapped or free-text LLM output is unpredictable and breaks downstream deserialization. Embedding the schema in the prompt (with an example input/output pair) maximally constrains the model. Two retries handle transient format errors without infinite loops.

**Alternative considered**: Structured output / response format enforcement via API parameter. Not used — SK's Azure OpenAI integration doesn't uniformly expose this parameter across versions; prompt-based enforcement is portable.

---

### 4. `System.CommandLine` beta4 for CLI surface

**Decision**: Use `System.CommandLine` beta4 with a `RootCommand` + `SpecCommand` structure.

**Rationale**: Matches the brief requirement. Beta4 is the most stable pre-release with the handler model that `SpecCommand` needs. The API surface won't change dramatically before GA.

---

### 5. `backlog-2-spec.json` loaded by upward search

**Decision**: `ConfigLoader` walks from the current working directory upward until it finds `backlog-2-spec.json` or reaches the filesystem root, then fails fast if not found or if required fields are missing.

**Rationale**: Mirrors the pattern used by `.editorconfig`, `tsconfig.json`, etc. — developers expect tool config to be discoverable without specifying explicit paths. Upward search supports monorepos where the tool is invoked from subdirectories.

---

### 6. HTML stripping via `HtmlAgilityPack`, not regex

**Decision**: Use `HtmlAgilityPack` to parse and strip HTML from ADO field values (Description, AcceptanceCriteria).

**Rationale**: ADO returns rich HTML in description fields. Regex stripping is fragile (the brief explicitly prohibits "regex hacks"). HtmlAgilityPack is the standard .NET HTML parser; it handles malformed HTML safely and produces clean plain-text output.

---

### 7. Temperature 0.1 for all LLM calls

**Decision**: Both agents use temperature 0.1 via `KernelFactory`.

**Rationale**: Deterministic output is a stated requirement. Low temperature minimizes variance in spec structure across runs on the same input, making the tool predictable and trustworthy in CI/review workflows.

## Risks / Trade-offs

- **ADO HTML format variability** → `HtmlAgilityPack` handles malformed HTML gracefully; add null-safety on all field reads
- **LLM JSON non-compliance after 2 retries** → Surface a typed `LlmFormatException` with the raw response; `SpecCommand` catches and displays it cleanly via `OutputRenderer`
- **Azure OpenAI quota/latency** → Progress steps shown via Spectre.Console give feedback; no timeout is set in Phase 1 (add in Phase 2 if needed)
- **Semantic Kernel API churn (pre-1.0)** → Pin a specific SK version; wrap SK calls behind `IEnrichmentAgent` / `ISpecGeneratorAgent` interfaces so future upgrades are localized
- **`dotnet user-secrets` requires a project UserSecretsId** → Must be set in `.csproj`; `ConfigLoader` fails fast if secrets are missing with a human-readable error

## Open Questions

- Should `--raw` output pretty-printed or compact JSON? (Defaulting to pretty-printed for readability; can be changed without breaking changes)
- Phase 2 will need a `plan` command — should `KernelFactory` become a shared service in an `Backlog2SpecAgent.Core` library at that point? (Yes — defer to Phase 2)
