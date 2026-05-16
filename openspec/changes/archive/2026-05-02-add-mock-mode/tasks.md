## 1. Mock Agents and ADO Client

- [x] 1.1 Create `src/Backlog2SpecAgent.Cli/Agents/MockEnrichmentAgent.cs` implementing `IEnrichmentAgent` with hardcoded `EnrichedTicket` (MissingAcceptanceCriteria: ["Validation rules not defined"], EdgeCases: ["Null input", "Large payload", "Timeout handling"], Constraints: ["Must support .NET 8", "REST API only"], AffectedComponents: ["API", "Application Layer"], Ambiguities: ["Expected max payload size unknown"])
- [x] 1.2 Create `src/Backlog2SpecAgent.Cli/Agents/MockSpecGeneratorAgent.cs` implementing `ISpecGeneratorAgent` with hardcoded `GeneratedSpec` (Summary: "Mock spec for testing pipeline", AcceptanceCriteria: 2 Gherkin entries, EdgeCases: ["Null input", "Extremely large payload"], OutOfScope: "Authentication, Authorization", ComponentBreakdown: ["API Endpoint", "Application Service", "Validation Layer"])
- [x] 1.3 Create `src/Backlog2SpecAgent.Cli/Ado/MockAdoClient.cs` implementing `IAdoClient` returning `WorkItemDto` with `Id` from input, Title: "Mock Work Item", Description: "This is a mock description", AcceptanceCriteria: "Sample acceptance criteria", WorkItemType: "User Story"

## 2. CLI Flag

- [x] 2.1 Add `--mock` `Option<bool>` to `SpecCommand` constructor and include it in `AddOption` and the `SetHandler` binding (update to 4-arg lambda or pre-parse via `args`)
- [x] 2.2 Thread the `mock` bool through `ExecuteAsync` signature and existing handler

## 3. DI Wiring

- [x] 3.1 In `Program.cs`, pre-parse `args` with `args.Contains("--mock")` before `Host.CreateDefaultBuilder` to detect mock mode
- [x] 3.2 Branch DI registration: if mock mode, register `MockEnrichmentAgent`, `MockSpecGeneratorAgent`, `MockAdoClient`; otherwise register real implementations as today
- [x] 3.3 After host is built, log `[MOCK MODE ENABLED]` at `Information` level when mock mode is active (use host's logger factory or a temporary logger)

## 4. Build Verification

- [x] 4.1 Run `dotnet build` and confirm 0 errors and 0 warnings
- [x] 4.2 Run `dotnet run --project src/Backlog2SpecAgent.Cli -- spec 99 --mock` and confirm the full pipeline renders a mock spec to the terminal with exit code 0
- [x] 4.3 Run `dotnet run --project src/Backlog2SpecAgent.Cli -- spec 99 --mock --raw` and confirm stdout is valid JSON only
- [x] 4.4 Run `dotnet run --project src/Backlog2SpecAgent.Cli -- spec 99 --mock --verbose` and confirm enrichment detail section appears
