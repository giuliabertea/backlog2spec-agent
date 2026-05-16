## 1. Remove Wiki Infrastructure

- [x] 1.1 Delete `src/Backlog2SpecAgent.Cli/Ado/WikiPageDto.cs`
- [x] 1.2 Delete `src/Backlog2SpecAgent.Cli/Agents/IWikiContextAgent.cs`
- [x] 1.3 Delete `src/Backlog2SpecAgent.Cli/Agents/WikiContextAgent.cs`
- [x] 1.4 Delete `src/Backlog2SpecAgent.Cli/Agents/MockWikiContextAgent.cs`

## 2. Add Codebase Context DTOs and Interface

- [x] 2.1 Create `src/Backlog2SpecAgent.Cli/Ado/CodeFileDto.cs` with properties `Path`, `FileName`, `Content` (all string)
- [x] 2.2 Create `src/Backlog2SpecAgent.Cli/Agents/ICodebaseContextAgent.cs` with method `FetchRelevantFilesAsync(WorkItemDto, AgentConfig, CancellationToken)`

## 3. Implement CodebaseContextAgent

- [x] 3.1 Create `src/Backlog2SpecAgent.Cli/Agents/CodebaseContextAgent.cs` — constructor accepts `string pat` and `ILogger<CodebaseContextAgent>`
- [x] 3.2 Implement `FetchRelevantFilesAsync`: return empty list immediately if `config.Ado.RepoName` is null/empty
- [x] 3.3 Implement ADO Git Items API call: `GET {org}/{project}/_apis/git/repositories/{repoName}/items?recursionLevel=full&versionDescriptor.version={branch}&api-version=7.1`
- [x] 3.4 Implement file path collection: traverse the API response and collect all file paths (items with `gitObjectType == "blob"`)
- [x] 3.5 Implement `ExtractKeywords`: split ticket title on non-word chars, lowercase, filter stop words and tokens < 4 chars, take up to 5 distinct terms
- [x] 3.6 Implement `ScorePath`: count keyword substrings in the file path (case-insensitive)
- [x] 3.7 Implement extension whitelist filter: allow only `.cs`, `.ts`, `.js`, `.py`, `.java`, `.go`, `.md`
- [x] 3.8 Implement content fetch: `GET {org}/{project}/_apis/git/repositories/{repoName}/items?path={encoded}&includeContent=true&api-version=7.1`, truncate to 800 chars
- [x] 3.9 Wrap all HTTP calls in try/catch — on any exception log warning and return empty list

## 4. Implement MockCodebaseContextAgent

- [x] 4.1 Create `src/Backlog2SpecAgent.Cli/Agents/MockCodebaseContextAgent.cs` implementing `ICodebaseContextAgent`
- [x] 4.2 Return a hardcoded list with one `CodeFileDto` containing a plausible C# class snippet (path, filename, short content)

## 5. Update Config Model

- [x] 5.1 In `src/Backlog2SpecAgent.Cli/Config/AgentConfig.cs`: remove `WikiName` from `AdoConfig`, add `RepoName` (string?) and `Branch` (string?)

## 6. Update EnrichmentAgent Interface and Implementation

- [x] 6.1 In `IEnrichmentAgent.cs`: change third parameter from `IReadOnlyList<WikiPageDto>` to `IReadOnlyList<CodeFileDto>`, rename to `codebaseContext`
- [x] 6.2 In `EnrichmentAgent.cs`: update `EnrichAsync` signature to match interface
- [x] 6.3 In `EnrichmentAgent.cs`: update `BuildPrompt` — replace wiki placeholder logic with codebase context formatting (`File: {Path}\n---\n{Content}`)
- [x] 6.4 In `MockEnrichmentAgent.cs`: update `EnrichAsync` signature to match interface

## 7. Update Prompt Template

- [x] 7.1 In `enrichment.txt`: rename `## Relevant Wiki Context` to `## Codebase Context`
- [x] 7.2 In `enrichment.txt`: update the section instruction text to reference source files instead of wiki pages

## 8. Update SpecCommand and DI Registration

- [x] 8.1 In `SpecCommand.cs`: replace `IWikiContextAgent _wikiContextAgent` field with `ICodebaseContextAgent _codebaseContextAgent`
- [x] 8.2 In `SpecCommand.cs`: rename `FetchRelevantPagesAsync` call to `FetchRelevantFilesAsync`, update progress message to "Fetching codebase context..."
- [x] 8.3 In `Program.cs`: replace `IWikiContextAgent`/`WikiContextAgent` registrations with `ICodebaseContextAgent`/`CodebaseContextAgent` in both real and mock branches

## 9. Update Configuration Example

- [x] 9.1 In `backlog-2-spec.json`: remove `"wikiName"` field, add `"repoName": null` and `"branch": null`

## 10. Verify

- [x] 10.1 Run `dotnet build src/Backlog2SpecAgent.Cli` — zero errors
- [x] 10.2 Run `dotnet backlog-2-spec spec 42 --mock` — pipeline completes successfully end-to-end
- [x] 10.3 Run `dotnet test tests/Backlog2SpecAgent.Tests` — all tests pass
