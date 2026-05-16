## 1. Solution & Project Setup

- [x] 1.1 Create solution: `dotnet new sln -n Backlog2SpecAgent` inside `Backlog2SpecAgent/`
- [x] 1.2 Create CLI project: `dotnet new console -n Backlog2SpecAgent.Cli -o src/Backlog2SpecAgent.Cli --framework net8.0`
- [x] 1.3 Create test project: `dotnet new xunit -n Backlog2SpecAgent.Tests -o tests/Backlog2SpecAgent.Tests --framework net8.0`
- [x] 1.4 Add both projects to solution: `dotnet sln add src/Backlog2SpecAgent.Cli tests/Backlog2SpecAgent.Tests`
- [x] 1.5 Add project reference from Tests to Cli: `dotnet add tests/Backlog2SpecAgent.Tests reference src/Backlog2SpecAgent.Cli`
- [x] 1.6 Configure `Backlog2SpecAgent.Cli.csproj`: set `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>backlog-2-spec</ToolCommandName>`, `<Version>0.1.0</Version>`, `<UserSecretsId>` (generate a GUID)
- [x] 1.7 Create all subdirectory stubs: `Commands/`, `Agents/`, `Ado/`, `Models/`, `Config/`, `Kernel/`, `Output/`, `Prompts/`

## 2. NuGet Dependencies

- [x] 2.1 Add `Microsoft.SemanticKernel` to `Backlog2SpecAgent.Cli`
- [x] 2.2 Add `Microsoft.TeamFoundationServer.Client` (Azure DevOps SDK) to `Backlog2SpecAgent.Cli`
- [x] 2.3 Add `System.CommandLine` (2.0.0-beta4.x) to `Backlog2SpecAgent.Cli`
- [x] 2.4 Add `Spectre.Console` to `Backlog2SpecAgent.Cli`
- [x] 2.5 Add `HtmlAgilityPack` to `Backlog2SpecAgent.Cli`
- [x] 2.6 Add `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.DependencyInjection` to `Backlog2SpecAgent.Cli`
- [x] 2.7 Run `dotnet restore` and verify no package conflicts

## 3. Configuration & Secrets

- [x] 3.1 Create `backlog-2-spec.json` at `Backlog2SpecAgent/` root with the specified schema (use placeholder org/project values)
- [x] 3.2 Implement `AgentConfig.cs` — POCO with nested `ProjectConfig`, `ConventionsConfig`, `AdoConfig` classes; all properties non-nullable strings
- [x] 3.3 Implement `ConfigLoader.cs` — upward search for `backlog-2-spec.json`, `System.Text.Json` deserialization with camelCase policy, required-field validation, throws typed `ConfigException`
- [x] 3.4 Define `ConfigException` (custom exception class in `Config/`)
- [x] 3.5 Run `dotnet user-secrets init --project src/Backlog2SpecAgent.Cli`
- [x] 3.6 Set placeholder secrets: `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, `AzureOpenAI:DeploymentName`, `Ado:Pat`

## 4. Domain Models

- [x] 4.1 Implement `WorkItemDto.cs` — `Id`, `Title`, `WorkItemType`, `Description`, `AcceptanceCriteria` (all non-nullable strings; `Id` is int)
- [x] 4.2 Implement `EnrichedTicket.cs` — `WorkItemId` (int), `Title`, plus `List<string>` for: `MissingAcceptanceCriteria`, `EdgeCases`, `Constraints`, `AffectedComponents`, `Ambiguities`
- [x] 4.3 Implement `GeneratedSpec.cs` — `Summary`, `OutOfScope` (strings); `AcceptanceCriteria`, `EdgeCases`, `ComponentBreakdown` (all `List<string>`)
- [x] 4.4 Annotate all models with `[JsonPropertyName]` attributes (camelCase) for serialization

## 5. ADO Client

- [x] 5.1 Define `IAdoClient.cs` interface with `Task<WorkItemDto> GetWorkItemAsync(int id, CancellationToken ct = default)`
- [x] 5.2 Implement `AdoClient.cs` — constructor injects `AgentConfig` and PAT string from DI; creates `VssConnection` with `VssBasicCredential`
- [x] 5.3 Call `GetWorkItemAsync(id, null, expand: WorkItemExpand.All)` and map the four required fields
- [x] 5.4 Use `HtmlAgilityPack` to strip HTML from `Description` and `AcceptanceCriteria` — helper private method `StripHtml(string html)`
- [x] 5.5 Map null field values to `string.Empty` (never return null strings)
- [x] 5.6 Define `AdoNotFoundException` and `AdoAuthException` (typed exceptions in `Ado/`)
- [x] 5.7 Wrap ADO SDK calls to catch `VssServiceException` and rethrow as typed exceptions with clean messages (no PAT in message)

## 6. Kernel Factory

- [x] 6.1 Implement `KernelFactory.cs` with static method `Build(string endpoint, string apiKey, string deploymentName): Kernel`
- [x] 6.2 Use `Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(...)` with temperature 0.1
- [x] 6.3 Return the fully configured `Kernel` instance

## 7. Prompt Files

- [x] 7.1 Create `Prompts/enrichment.txt` — instructs model to return only JSON matching `EnrichedTicket` schema; includes full JSON schema definition and one example input/output pair; instructs model not to guess missing information
- [x] 7.2 Create `Prompts/spec.txt` — instructs model to return only JSON matching `GeneratedSpec` schema; specifies fixed section order (summary, acceptanceCriteria, edgeCases, outOfScope, componentBreakdown); Gherkin format for acceptanceCriteria; no extra sections

## 8. Enrichment Agent

- [x] 8.1 Define `IEnrichmentAgent.cs` interface with `Task<EnrichedTicket> EnrichAsync(WorkItemDto workItem, AgentConfig config, CancellationToken ct = default)`
- [x] 8.2 Implement `EnrichmentAgent.cs` — constructor injects `Kernel`, `ILogger<EnrichmentAgent>`
- [x] 8.3 Load `enrichment.txt` prompt from embedded resource or file path; inject work item fields into prompt
- [x] 8.4 Call SK chat completion; parse response with `System.Text.Json`; implement retry loop (max 2 retries on `JsonException`)
- [x] 8.5 Define `LlmFormatException` (typed exception in `Agents/`) including raw LLM response in message
- [x] 8.6 Throw `LlmFormatException` after all retries exhausted
- [x] 8.7 Add `ILogger` calls: Info at start/end, Debug for response character count

## 9. Spec Generator Agent

- [x] 9.1 Define `ISpecGeneratorAgent.cs` interface with `Task<GeneratedSpec> GenerateAsync(EnrichedTicket enriched, AgentConfig config, CancellationToken ct = default)`
- [x] 9.2 Implement `SpecGeneratorAgent.cs` — constructor injects `Kernel`, `ILogger<SpecGeneratorAgent>`
- [x] 9.3 Load `spec.txt` prompt; inject `EnrichedTicket` fields into prompt
- [x] 9.4 Call SK chat completion; parse with `System.Text.Json`; implement retry loop (max 2 retries)
- [x] 9.5 Throw `LlmFormatException` after all retries exhausted
- [x] 9.6 Add `ILogger` calls: Info at start/end, Debug for response size

## 10. Output Renderer

- [x] 10.1 Create `IOutputRenderer.cs` interface with: `RenderProgress(string step)`, `RenderSpec(GeneratedSpec spec, bool verbose)`, `RenderError(string message)`, `RenderRaw(GeneratedSpec spec)`
- [x] 10.2 Implement `OutputRenderer.cs` using Spectre.Console; no direct `Console.Write` calls
- [x] 10.3 Implement progress steps using `AnsiConsole.Status()` or `AnsiConsole.Progress()`
- [x] 10.4 Implement color scheme: headers in blue, body in white, edge cases in yellow, ambiguities in red
- [x] 10.5 Implement `RenderRaw` — serialize `GeneratedSpec` to indented JSON using `System.Text.Json` and write to stdout (suppress all color/formatting)
- [x] 10.6 Implement `RenderError` — display message in red via `AnsiConsole.MarkupLine`

## 11. Spec Command

- [x] 11.1 Implement `SpecCommand.cs` — `Command` subclass named `spec` with required `<id>` argument (int), `--verbose` option (bool), `--raw` option (bool)
- [x] 11.2 Inject `IAdoClient`, `IEnrichmentAgent`, `ISpecGeneratorAgent`, `IOutputRenderer`, `ConfigLoader`, `ILogger<SpecCommand>` via constructor
- [x] 11.3 Implement `ExecuteAsync` handler: load config → fetch → enrich → generate → render (or render raw)
- [x] 11.4 Wrap entire pipeline in try/catch; catch `AdoNotFoundException`, `AdoAuthException`, `LlmFormatException`, `ConfigException` individually with typed messages; catch `Exception` as fallback
- [x] 11.5 Call `RenderError` via `IOutputRenderer` for all error paths; return non-zero exit code
- [x] 11.6 Return exit code 0 on success

## 12. DI Wiring & Program.cs

- [x] 12.1 Implement `Program.cs` — configure `IHostBuilder` with DI container
- [x] 12.2 Register `IAdoClient → AdoClient`, `IEnrichmentAgent → EnrichmentAgent`, `ISpecGeneratorAgent → SpecGeneratorAgent`, `IOutputRenderer → OutputRenderer`, `ConfigLoader` as singletons/scoped as appropriate
- [x] 12.3 Bind user-secrets into `IConfiguration`; pass secrets to `KernelFactory.Build(...)` and `AdoClient` constructor
- [x] 12.4 Build `RootCommand` → `SpecCommand`; invoke with `args`
- [x] 12.5 Ensure `Program` is the only static class in the solution

## 13. Test Project Scaffolding

- [x] 13.1 Create `Backlog2SpecAgent.Tests/AdoClientTests.cs` — empty test class with one `[Fact]` placeholder
- [x] 13.2 Create `Backlog2SpecAgent.Tests/EnrichmentAgentTests.cs` — empty test class with one `[Fact]` placeholder
- [x] 13.3 Create `Backlog2SpecAgent.Tests/SpecGeneratorAgentTests.cs` — empty test class with one `[Fact]` placeholder
- [x] 13.4 Create `Backlog2SpecAgent.Tests/ConfigLoaderTests.cs` — empty test class with one `[Fact]` placeholder
- [x] 13.5 Create `Backlog2SpecAgent.Tests/OutputRendererTests.cs` — empty test class with one `[Fact]` placeholder
- [x] 13.6 Run `dotnet test` and confirm all placeholder tests pass

## 14. Build & Smoke Test

- [x] 14.1 Run `dotnet build` — zero warnings, zero errors
- [x] 14.2 Create `dotnet-tools.json` manifest: `dotnet new tool-manifest`
- [x] 14.3 Install tool locally: `dotnet tool install --local Backlog2SpecAgent.Cli` (or pack and install)
- [x] 14.4 Run `dotnet tool run backlog-2-spec spec 12345` with placeholder secrets — confirm it reaches ADO (or fails cleanly with auth error, not a crash)
- [x] 14.5 Run `dotnet tool run backlog-2-spec spec 12345 --raw` — confirm JSON-only stdout

## 15. README

- [x] 15.1 Write `README.md` covering: prerequisites (.NET 8 SDK), installation (`dotnet tool install`), config setup (`backlog-2-spec.json`), secrets setup (`dotnet user-secrets`), usage (`backlog-2-spec spec <id>`), troubleshooting (common errors and fixes)
