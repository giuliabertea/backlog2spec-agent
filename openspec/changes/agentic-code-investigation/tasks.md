## 1. Tools API — add navigation endpoints

- [x] 1.1 Add `Swashbuckle.AspNetCore` NuGet to
      `src/Backlog2SpecAgent.Tools/Backlog2SpecAgent.Tools.csproj`
- [x] 1.2 Register `AddEndpointsApiExplorer()` and `AddSwaggerGen(c => { c.SwaggerDoc("v1", ...) })`
      in `builder.Services` in `Program.cs`
- [x] 1.3 Add `app.UseSwagger()` and `app.UseSwaggerUI()` after `app.Build()`; exclude
      `/swagger/*` paths from the `X-Api-Key` middleware check
- [x] 1.4 Assign `operationId` + `summary` + `description` to existing endpoints via
      `.WithOpenApi(...)`: `getWorkItem`, `getWorkItemHierarchy`, `searchCode`, `saveSpec`
- [x] 1.5 Implement `GET /repo/tree?path={dir}` — list files and folders one level deep
      in the configured ADO repo/branch; return `[{ path, type }]`;
      assign `operationId = "listDirectory"`
- [x] 1.6 Implement `GET /repo/file?path={path}&startLine={n}&endLine={m}` — return
      line-prefixed content of the requested range (max 400 lines); return
      `{ path, startLine, endLine, totalLines, content }`;
      assign `operationId = "readFile"`
- [x] 1.7 Implement `POST /repo/references { "symbol": "string" }` — whole-word
      case-sensitive regex search across source files (`.cs`, `.ts`, `.js`,
      `.py`, `.java`, `.go`); return `[{ path, line, snippet }]` capped at 50;
      assign `operationId = "findReferences"`
- [x] 1.8 Implement `GET /repo/outline?path={path}` — regex extraction of namespaces,
      classes/records/interfaces, public method signatures and line numbers from
      `.cs` files; return `{ path, symbols: [{ kind, name, signature, line }] }`;
      assign `operationId = "getFileOutline"`
- [x] 1.9 Verify that `GET https://localhost:PORT/swagger/v1/swagger.json` returns a
      valid OpenAPI 3.0 document containing all eight `operationId` values

## 2. Extend the spec schema

- [x] 2.1 In `src/Backlog2SpecAgent.Cli/Models/GeneratedSpec.cs` transform
      `FilesToChange` from `List<string>` to `List<FileChange>` where `FileChange`
      is a new `sealed class` with properties `File`, `Change`, `Evidence`,
      `Confidence` (all `string`, camelCase JSON); add `OpenQuestions : List<string>`
      and `Conventions : List<string>` (both default `[]`)
- [x] 2.2 In `src/Backlog2SpecAgent.Cli/Agents/FoundrySpecGeneratorAgent.cs` update the
      private `FoundrySpec` class and `FoundryFileChange` class to match the new
      schema (`evidence`, `confidence` fields); update `MapToGeneratedSpec` to
      map all new fields
- [x] 2.3 Update `src/Backlog2SpecAgent.Cli/Output/OutputRenderer.cs`:
      - `RenderSpec`: for each file show `file`, `change`, evidence (dimmed), and
        a confidence badge (`[green]●[/]` high / `[yellow]●[/]` medium /
        `[red]●[/]` low) using Spectre.Console markup
      - Add a section `── Open Questions ─` if `OpenQuestions` is non-empty
      - Add a section `── Conventions ─` if `Conventions` is non-empty
      - `RenderMarkdown` / `WriteHierarchyToFiles`: include all new fields
- [x] 2.4 Update `MockSpecGeneratorAgent` to produce a valid `GeneratedSpec` with the
      new schema (non-empty `Evidence`, `Confidence = "high"`)
- [x] 2.5 Update `tests/Backlog2SpecAgent.Tests/OutputRendererTests.cs` for the new
      `FileChange` type and new sections
- [x] 2.6 Update `tests/Backlog2SpecAgent.Tests/SpecGeneratorAgentTests.cs` to assert
      on `Evidence` and `Confidence` fields in the parsed output

## 3. Simplify FoundrySpecGeneratorAgent — remove C#-side retrieval

- [x] 3.1 Add config key `AzureSearch:UseClientSideRetrieval` (bool, default `false`)
      to `src/Backlog2SpecAgent.Cli/Config/AgentConfig.cs` and `ConfigLoader.cs`
- [x] 3.2 In `FoundrySpecGeneratorAgent.GenerateAsync`: gate the `QueryAzureSearchAsync`
      call and `repoContext` payload field behind `UseClientSideRetrieval`; when
      `false`, send `{ workItem, projectConfig, devRules }` only
- [x] 3.3 Update `BuildPayload` to omit `repoContext` when `UseClientSideRetrieval` is
      `false` (or when the list is empty)
- [x] 3.4 Update DI wiring in `src/Backlog2SpecAgent.Cli/Program.cs` if the
      `FoundrySpecGeneratorAgent` constructor signature changes
- [x] 3.5 Confirm `dotnet build` passes with no errors or warnings

## 4. Update README

- [x] 4.1 Add `AzureSearch:UseClientSideRetrieval` to the user-secrets table with
      description `"false" (default) — agent fetches repo context via tools`
- [x] 4.2 Add a section **Step B: Register the Tools API as an OpenAPI tool in Foundry**
      pointing to `/swagger/v1/swagger.json` and documenting the X-Api-Key
      authentication setting in the portal
- [x] 4.3 Add a section **Step C: Replace the agent system prompt** with a note to
      paste the investigation protocol from the design doc
- [x] 4.4 Document the new `filesToChange` schema fields (`evidence`, `confidence`)
      in the Output section of the README

## 5. Verify end-to-end

- [x] 5.1 Run `dotnet build` on the full solution — no errors
- [ ] 5.2 Run `dotnet backlog-2-spec spec <id> --mock` — confirm mock output renders
      the new schema fields correctly and no exception is thrown
- [x] 5.3 Run `dotnet test` — all tests pass
- [ ] 5.4 Deploy updated Tools API to App Service; confirm
      `/swagger/v1/swagger.json` is accessible and lists all eight operations
- [ ] 5.5 In Foundry portal: import the OpenAPI document as a tool on
      `backlog2spec-agent`; confirm all operations appear in the tool panel
- [ ] 5.6 In Foundry playground: send a test payload and verify in the trace that
      the agent calls at least `searchCode` + `readFile` before producing the JSON
- [ ] 5.7 Run `dotnet backlog-2-spec spec <id>` against a real work item; confirm
      `filesToChange` entries all have non-empty `evidence` and that the displayed
      confidence badges are correct
- [ ] 5.8 Compare output against pre-change baseline (5 PBIs): verify path accuracy
      improves and `openQuestions` captures genuine ambiguities
