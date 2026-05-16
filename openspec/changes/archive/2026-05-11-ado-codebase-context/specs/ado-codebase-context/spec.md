## ADDED Requirements

### Requirement: List repository files via ADO Git Items API
`CodebaseContextAgent` SHALL call `GET {org}/{project}/_apis/git/repositories/{repoName}/items?recursionLevel=full&api-version=7.1` using Basic auth (PAT) to retrieve the full file tree of the configured repository. If `ado.repoName` is absent or empty in `AgentConfig`, the agent SHALL return an empty list immediately without making any HTTP call.

#### Scenario: RepoName absent returns empty list without HTTP call
- **WHEN** `FetchRelevantFilesAsync` is called and `config.Ado.RepoName` is null or empty
- **THEN** an empty `IReadOnlyList<CodeFileDto>` is returned and no HTTP request is made

#### Scenario: Repository listed successfully
- **WHEN** `config.Ado.RepoName` is set and the ADO API returns a valid file tree
- **THEN** the agent proceeds to score and fetch content from matching files

### Requirement: Score file paths by keyword relevance
`CodebaseContextAgent` SHALL extract keywords from the `WorkItemDto` title by splitting on non-word characters, lowercasing, removing stop words, removing tokens shorter than 4 characters, and taking up to 5 distinct terms. It SHALL score each file path by counting how many keywords appear as substrings (case-insensitive) in the path string.

#### Scenario: File path matching ticket keyword scores above zero
- **WHEN** the ticket title contains "Profitability" and a file path contains "profitability"
- **THEN** that file path receives a score of at least 1

#### Scenario: File path with no keyword match scores zero and is excluded
- **WHEN** no keyword from the ticket title appears in a file path
- **THEN** that file is not included in the fetched results

### Requirement: Fetch content of top-scoring source files
`CodebaseContextAgent` SHALL fetch file content for the top 3 file paths (by descending score, ties broken by path length ascending). Only files whose extension is in the whitelist (`.cs`, `.ts`, `.js`, `.py`, `.java`, `.go`, `.md`) SHALL be considered. Content SHALL be truncated to 800 characters. Each result SHALL be returned as a `CodeFileDto` with `Path`, `FileName` (last path segment), and `Content`.

#### Scenario: Non-source files excluded from results
- **WHEN** the highest-scoring paths include `.json`, `.csproj`, or binary files
- **THEN** those paths are excluded and the next-highest source files are selected instead

#### Scenario: Content truncated at 800 characters
- **WHEN** a matched file contains more than 800 characters
- **THEN** `CodeFileDto.Content` contains the first 800 characters followed by `"..."`

#### Scenario: Fewer than 3 matches returns all available
- **WHEN** only 1 file path scores above zero
- **THEN** the agent returns a list with exactly 1 `CodeFileDto`

### Requirement: Graceful degradation on any API error
`CodebaseContextAgent` SHALL wrap all HTTP calls in a try/catch. On any exception (network error, 401, 404, timeout), it SHALL log a warning via `ILogger` and return an empty list. The exception SHALL NOT propagate to the caller.

#### Scenario: HTTP 401 returns empty list with warning log
- **WHEN** the ADO API returns 401 Unauthorized
- **THEN** `FetchRelevantFilesAsync` returns an empty list and logs a warning containing "codebase context"

#### Scenario: Network timeout returns empty list
- **WHEN** the HTTP call throws `TaskCanceledException` or `HttpRequestException`
- **THEN** `FetchRelevantFilesAsync` returns an empty list without throwing
