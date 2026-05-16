## Context

`backlog-2-spec` currently has a single execution path that always calls Azure DevOps and Azure OpenAI. There is no way to exercise the pipeline without live credentials. The existing DI setup in `Program.cs` registers concrete implementations unconditionally, and `SpecCommand` receives `IEnrichmentAgent`, `ISpecGeneratorAgent`, and `IAdoClient` — all interfaces — which makes swapping implementations straightforward.

The challenge is that the `--mock` flag is a CLI option that must influence DI registration, but DI is configured before the command runs. The solution is a pre-parse of `args` to detect `--mock`, then branch the DI setup accordingly.

## Goals / Non-Goals

**Goals:**
- Enable `backlog-2-spec spec <id> --mock` to run end-to-end with no external calls
- Produce identical output on every invocation (fully deterministic)
- Route all output through the existing `OutputRenderer` — no mock-specific rendering
- Keep real implementations completely untouched

**Non-Goals:**
- Configurable mock data (hardcoded values only in Phase 1)
- Selective mocking (e.g., mock LLM but use real ADO) — `--mock` mocks everything
- Mock data loaded from files
- Test project changes (mocks are production code usable in tests, but test wiring is out of scope)

## Decisions

### Decision 1: Pre-parse `args` to detect `--mock` before DI setup

**Chosen:** `args.Contains("--mock")` check before `Host.CreateDefaultBuilder`.

**Why:** `System.CommandLine` beta4 parses args only after the host is built and `InvokeAsync` is called. DI must be configured at host build time. Pre-parsing with a simple `Contains` check is reliable because `--mock` has no value argument and cannot appear as a positional value.

**Alternative considered:** Build the host twice (once with real deps, once with mocks). Rejected — unnecessary complexity.

### Decision 2: `--mock` implies both LLM mock and ADO mock

**Chosen:** A single `--mock` flag mocks all three external interfaces (`IAdoClient`, `IEnrichmentAgent`, `ISpecGeneratorAgent`).

**Why:** The goal is "run without any credentials." Partial mocking (e.g., real ADO + mock LLM) would still require secrets and defeats the purpose. Keeping it as a single flag avoids combinatorial complexity.

**Alternative considered:** Separate `--mock-llm` and `--mock-ado` flags. Rejected for Phase 1 — YAGNI; can be added later as non-breaking additions.

### Decision 3: Mock implementations as production code, not test doubles

**Chosen:** `MockEnrichmentAgent`, `MockSpecGeneratorAgent`, `MockAdoClient` live in `src/Backlog2SpecAgent.Cli/`, not in the test project.

**Why:** They are referenced by `Program.cs` at runtime and must be packaged with the tool. The test project can reference them, but they are not test infrastructure.

### Decision 4: DI logging via `ILogger` for mock mode banner

**Chosen:** Log `[MOCK MODE ENABLED]` at `Information` level in `Program.cs` after services are registered.

**Why:** Logging is the established convention for runtime mode indicators. The logging minimum level is `Warning` by default, so this message is silent in normal use. To see it, the user would need to lower the log level — acceptable for a developer-facing flag.

**Alternative considered:** Print directly to console via `AnsiConsole` before the command runs. Rejected — `OutputRenderer` is the sole console writer per architecture rules. A progress line through `OutputRenderer` would be better, but `OutputRenderer` isn't available outside the command. Logging is the correct channel.

## Risks / Trade-offs

- **Pre-parse fragility**: `args.Contains("--mock")` will misfire if a work item title somehow contains `--mock` as an argument. Mitigated — positional values are integers, so string `--mock` can only appear as a named option.
- **Hardcoded mock data divergence**: If the real `EnrichedTicket` or `GeneratedSpec` models add required fields, mock implementations won't be updated automatically. Mitigation: mocks use object initializers — missing required properties cause compile errors, surfacing the issue at build time.
- **No selective mocking**: Users who want to test with real ADO but mocked LLM must use real credentials. Accepted trade-off for Phase 1 simplicity.

## Migration Plan

No migration required. The `--mock` flag is purely additive. Existing invocations without the flag behave identically to today.
