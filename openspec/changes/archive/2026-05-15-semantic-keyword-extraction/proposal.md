## Why

The current keyword extractor splits the work item title on whitespace, strips stop words, and takes the first 5 tokens. For concrete tickets ("fix ProfitabilityReport null ref") this works well, but for abstract ones ("add observability to the ingestion pipeline") it produces generic terms like `observ`, `ingest`, `pipelin` that match very few file paths. Replacing it with a single LLM call lets the model infer domain-specific class names and identifiers that actually appear in source code, dramatically improving file relevance with minimal added cost.

## What Changes

- `CodebaseContextAgent` will call the LLM once per run before scoring file paths, asking it to produce 5–8 technical terms and class-name fragments relevant to the work item title and description.
- The existing stop-word / token-splitting logic is removed and replaced by the LLM-derived keyword list.
- The rest of the scoring and file-fetch pipeline (substring match, top-3 selection, content truncation) is unchanged.

## Capabilities

### New Capabilities

- `llm-keyword-extractor`: Standalone capability that accepts a work item title + description and returns a list of 5–8 technical search terms via a lightweight LLM call. Used by `CodebaseContextAgent` to replace the stop-word extractor.

### Modified Capabilities

- `ado-codebase-context`: The "Score file paths by keyword relevance" requirement changes — keywords are now produced by the `llm-keyword-extractor` instead of being derived by splitting and filtering the title string.

## Impact

- **Modified**: `CodebaseContextAgent` (keyword extraction logic replaced; LLM client injected).
- **New**: `IKeywordExtractor` / `LlmKeywordExtractor` (or equivalent abstraction).
- **Dependencies**: Requires access to the Azure AI connector already used by `SpecGeneratorAgent`.
- **Cost**: One additional token-light LLM call per `spec` run (input: ~100 tokens, output: ~30 tokens).
- **Testability**: `MockCodebaseContextAgent` / `MockKeywordExtractor` need no changes if interface is extracted correctly.
