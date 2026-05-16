## MODIFIED Requirements

### Requirement: Score file paths by keyword relevance
`CodebaseContextAgent` SHALL obtain the keyword list by calling `IKeywordExtractor.ExtractAsync(workItem, ct)` rather than deriving it from the title using stop-word splitting. It SHALL score each source file path by counting how many keywords appear as substrings (case-insensitive) in the path string. The remainder of the scoring and file-fetch pipeline (top-N selection, content truncation) is unchanged.

#### Scenario: File path matching LLM-derived keyword scores above zero
- **WHEN** `IKeywordExtractor` returns `["ingestion", "pipeline", "ILogger"]` and a file path contains "ingestion"
- **THEN** that file path receives a score of at least 1

#### Scenario: File path with no keyword match scores zero and is excluded
- **WHEN** no keyword from `IKeywordExtractor` appears in a file path
- **THEN** that file is not included in the fetched results

#### Scenario: Keyword extractor failure does not abort file fetch
- **WHEN** `IKeywordExtractor.ExtractAsync` returns an empty list (due to fallback)
- **THEN** `FetchRelevantFilesAsync` returns an empty list without throwing
