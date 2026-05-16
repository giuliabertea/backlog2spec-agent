## Why

Running `backlog-2-spec spec <id>` currently requires live Azure OpenAI and Azure DevOps credentials, making local development, CI smoke tests, and first-time onboarding impossible without real secrets. A `--mock` flag enables the full pipeline to execute with zero external calls, producing deterministic output every time.

## What Changes

- Add `--mock` CLI flag to the `spec` command
- Add `MockEnrichmentAgent` implementing `IEnrichmentAgent` with hardcoded, deterministic output
- Add `MockSpecGeneratorAgent` implementing `ISpecGeneratorAgent` with hardcoded, deterministic output
- Add `MockAdoClient` implementing `IAdoClient` with hardcoded, deterministic output
- Update DI registration in `Program.cs` to switch to mock implementations when `--mock` is passed
- Log `[MOCK MODE ENABLED]` at Info level when mock mode is active

## Capabilities

### New Capabilities
- `mock-enrichment-agent`: Deterministic implementation of IEnrichmentAgent that returns fixed enrichment data without calling any LLM
- `mock-spec-generator-agent`: Deterministic implementation of ISpecGeneratorAgent that returns a fixed GeneratedSpec without calling any LLM
- `mock-ado-client`: Deterministic implementation of IAdoClient that returns a fixed WorkItemDto without calling Azure DevOps

### Modified Capabilities
- `spec-command`: Adds `--mock` option; when set, registers mock implementations instead of real ones and skips all external calls

## Impact

- `src/Backlog2SpecAgent.Cli/Commands/SpecCommand.cs` — no structural change; flag parsed and propagated to DI
- `src/Backlog2SpecAgent.Cli/Program.cs` — DI registration branched on `--mock`
- `src/Backlog2SpecAgent.Cli/Agents/MockEnrichmentAgent.cs` — new file
- `src/Backlog2SpecAgent.Cli/Agents/MockSpecGeneratorAgent.cs` — new file
- `src/Backlog2SpecAgent.Cli/Ado/MockAdoClient.cs` — new file
- No changes to real agent implementations, KernelFactory, or AdoClient
- No new NuGet dependencies
