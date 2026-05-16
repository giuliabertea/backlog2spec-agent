## Context

`CodebaseContextAgent` currently extracts keywords by splitting the work item title on delimiters, lowercasing, and removing stop words (see `ExtractKeywords` in `CodebaseContextAgent.cs:142`). For abstract tickets this produces stems that match few file paths. The `SpecGeneratorAgent` already has an established pattern for LLM calls via Semantic Kernel (`IChatCompletionService`). `Program.cs` already builds a `Kernel` for the non-mock path and passes it to the enrichment and spec-gen agents — the same `Kernel` can be reused here at no additional setup cost.

## Goals / Non-Goals

**Goals:**
- Replace the stopword extractor with a single token-light LLM call that produces domain-aware terms.
- Keep `CodebaseContextAgent` resilient: if the LLM call fails, fall back to the existing stopword logic so the pipeline never hard-fails on keyword extraction.
- Maintain the `--mock` path unchanged (mock substitutes the whole `ICodebaseContextAgent`).

**Non-Goals:**
- Changing the scoring, file-fetch, or content-truncation logic.
- Adding a new configuration knob to enable/disable LLM extraction.
- Building a general-purpose keyword service beyond what this use case needs.

## Decisions

### 1. Extract an `IKeywordExtractor` interface

**Decision**: introduce `IKeywordExtractor` with `Task<IReadOnlyList<string>> ExtractAsync(WorkItemDto, CancellationToken)`, and inject it into `CodebaseContextAgent`.

**Rationale**: keeps the LLM concern out of the file-fetching class, mirrors the `IEnrichmentAgent` / `ISpecGeneratorAgent` pattern already in the codebase, and makes unit testing straightforward.

**Alternative considered**: pass `Kernel` directly into `CodebaseContextAgent`. Rejected — it leaks the LLM abstraction into a class whose responsibility is HTTP file retrieval.

### 2. `LlmKeywordExtractor` wraps `StopwordKeywordExtractor` as fallback

**Decision**: `LlmKeywordExtractor` calls the LLM and, on any exception, logs a warning and delegates to the existing stopword logic (extracted to `StopwordKeywordExtractor`).

**Rationale**: a keyword-extraction failure should never abort a spec run. The stopword approach works well for concrete tickets and costs nothing, so it's a free safety net.

### 3. Prompt returns a raw JSON array

**Decision**: the LLM is asked to reply with *only* a JSON array of strings, no surrounding prose.

Example prompt:
```
Given this work item, list 5–8 technical terms and C# class-name fragments that are likely to appear in related source files. Reply with a JSON array of strings only, no explanation.

Title: {{title}}
Description: {{description}}
```

**Rationale**: mirrors the `SpecGeneratorAgent` pattern (JSON-only response, `JsonSerializer.Deserialize`). Temperature 0 is used for determinism.

**Alternative considered**: ask for comma-separated terms. Rejected — harder to parse reliably across different model outputs.

### 4. `LlmKeywordExtractor` registered via existing `Kernel`

**Decision**: in `Program.cs`, register `IKeywordExtractor` as `LlmKeywordExtractor` using `sp.GetRequiredService<Kernel>()` (same kernel already built for enrichment/spec-gen). `CodebaseContextAgent` constructor gains an `IKeywordExtractor` parameter.

**Rationale**: zero new configuration, zero new secrets.

## Risks / Trade-offs

- **Latency**: one extra LLM round-trip (~200–400 ms) added to each `spec` run. Acceptable — it runs in sequence with the existing enrichment call.
- **Cost**: ~130 input tokens + ~30 output tokens per run. Negligible.
- **LLM non-determinism**: two runs on identical tickets may produce slightly different keyword sets, leading to different file matches. Mitigated by temperature 0.
- **JSON parse failure**: model returns prose instead of a JSON array. `LlmKeywordExtractor` catches `JsonException` and falls back.
