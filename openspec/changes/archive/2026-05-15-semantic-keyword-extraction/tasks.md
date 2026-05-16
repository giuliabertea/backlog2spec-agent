## 1. Keyword Extractor Abstraction

- [x] 1.1 Create `IKeywordExtractor` interface in `Agents/` with `Task<IReadOnlyList<string>> ExtractAsync(WorkItemDto workItem, CancellationToken ct)`
- [x] 1.2 Extract existing stop-word logic from `CodebaseContextAgent.ExtractKeywords` into a new `StopwordKeywordExtractor : IKeywordExtractor` class
- [x] 1.3 Verify `StopwordKeywordExtractor` passes the existing keyword-extraction edge-case scenarios (empty title, short tokens, stop words)

## 2. LLM Keyword Extractor

- [x] 2.1 Create `LlmKeywordExtractor : IKeywordExtractor` accepting `Kernel` and `ILogger<LlmKeywordExtractor>`
- [x] 2.2 Implement the LLM prompt: title + description → JSON string array (temperature 0, single user message)
- [x] 2.3 Parse the JSON array response and return as `IReadOnlyList<string>`
- [x] 2.4 Wrap the LLM call in try/catch; on any exception log a warning and delegate to `StopwordKeywordExtractor.ExtractAsync`
- [x] 2.5 Handle the case where `WorkItemDto.Description` is null/empty (omit from prompt)

## 3. Wire Up CodebaseContextAgent

- [x] 3.1 Add `IKeywordExtractor` constructor parameter to `CodebaseContextAgent`
- [x] 3.2 Replace the `ExtractKeywords(workItem)` call with `await _keywordExtractor.ExtractAsync(workItem, ct)`
- [x] 3.3 Remove the static `Stopwords` array and `ExtractKeywords` method from `CodebaseContextAgent`

## 4. DI Registration

- [x] 4.1 Register `IKeywordExtractor` as `LlmKeywordExtractor` in the non-mock branch of `Program.cs` (reuse existing `Kernel`)
- [x] 4.2 Update the `ICodebaseContextAgent` factory registration to inject `IKeywordExtractor`
- [x] 4.3 Confirm mock branch unchanged (no `IKeywordExtractor` registration needed — `MockCodebaseContextAgent` substitutes the whole agent)

## 5. Smoke Test

- [x] 5.1 Run `dotnet build` and confirm no compilation errors
- [x] 5.2 Run with `--mock` flag and verify no regression in mock pipeline output
- [ ] 5.3 Run against a real abstract ticket (e.g., "add observability to ingestion") and confirm LLM-derived keywords appear in the debug log
