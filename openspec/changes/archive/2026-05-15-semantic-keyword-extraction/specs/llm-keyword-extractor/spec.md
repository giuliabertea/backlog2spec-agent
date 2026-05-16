## ADDED Requirements

### Requirement: Extract search keywords via LLM
`LlmKeywordExtractor` SHALL call the configured LLM with the work item title and description and ask it to return a JSON array of 5–8 technical terms and class-name fragments that are likely to appear in related source files. The LLM SHALL be invoked at temperature 0. The extractor SHALL return the parsed array as `IReadOnlyList<string>`.

#### Scenario: LLM returns valid JSON array
- **WHEN** the LLM responds with a valid JSON string array (e.g., `["ingestion","pipeline","ILogger","OpenTelemetry"]`)
- **THEN** `ExtractAsync` returns exactly those strings as the keyword list

#### Scenario: Work item has both title and description
- **WHEN** `WorkItemDto.Description` is non-empty
- **THEN** both title and description are included in the LLM prompt

#### Scenario: Work item has title only
- **WHEN** `WorkItemDto.Description` is null or empty
- **THEN** only the title is included in the LLM prompt and the call succeeds without error

### Requirement: Fall back to stopword extraction on LLM failure
`LlmKeywordExtractor` SHALL catch any exception thrown during the LLM call (network error, JSON parse failure, timeout) and log a warning via `ILogger`. It SHALL then delegate to `StopwordKeywordExtractor` and return its result, ensuring `ExtractAsync` never throws.

#### Scenario: LLM call throws exception
- **WHEN** the LLM call throws any `Exception`
- **THEN** `ExtractAsync` returns the stopword-derived keyword list and logs a warning

#### Scenario: LLM returns non-JSON prose
- **WHEN** the LLM response cannot be deserialized as a JSON string array
- **THEN** `ExtractAsync` falls back to `StopwordKeywordExtractor` and logs a warning

### Requirement: Stopword fallback extractor preserves existing logic
`StopwordKeywordExtractor` SHALL implement `IKeywordExtractor` and reproduce the original extraction behavior: split the work item title on non-word characters, lowercase tokens, remove stop words and tokens shorter than 4 characters, deduplicate, and return up to 5 terms.

#### Scenario: Title with stopwords and short tokens
- **WHEN** the title is "Fix the null ref in reporting"
- **THEN** the extractor returns terms like `["null", "reporting"]` (short and stop tokens excluded)

#### Scenario: Empty title returns empty list
- **WHEN** `WorkItemDto.Title` is null or empty
- **THEN** `StopwordKeywordExtractor` returns an empty list without throwing
