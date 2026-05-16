## 1. Core Infrastructure

- [x] 1.1 Create `Exceptions/` directory and add `BudgetExceededException.cs` with `decimal CurrentCost`, `decimal BudgetLimit` properties, and a `Message` that reads `$"Budget exceeded. Current cost: ${CurrentCost:F2}, Limit: ${BudgetLimit:F2}."`
- [x] 1.2 Create `Services/` directory and add `TokenUsageTracker.cs` with `private long _inputTokens` and `private long _outputTokens` fields initialized to zero
- [x] 1.3 Implement `AddUsage(int inputTokens, int outputTokens)`: clamp both arguments to `Math.Max(0, ...)`, then call `Interlocked.Add(ref _inputTokens, inputTokens)` and the same for output
- [x] 1.4 Add pricing constants `private const decimal InputCostPerMillionTokens = 2.50m` and `private const decimal OutputCostPerMillionTokens = 10.00m`; implement `GetEstimatedCost()` returning `(_inputTokens / 1_000_000m * InputCostPerMillionTokens) + (_outputTokens / 1_000_000m * OutputCostPerMillionTokens)`
- [x] 1.5 Add `decimal BudgetLimit { get; set; } = 20m` property
- [x] 1.6 Implement `bool IsBudgetExceeded(decimal limit)` returning `GetEstimatedCost() >= limit`; implement `void EnforceBudget()` calling `if (IsBudgetExceeded(BudgetLimit)) throw new BudgetExceededException(GetEstimatedCost(), BudgetLimit)`

## 2. Dependency Injection

- [x] 2.1 In `Program.cs`, add `services.AddSingleton<TokenUsageTracker>()` before any agent registrations — it must appear in both the mock-mode and real-mode branches so it is always resolvable

## 3. EnrichmentAgent Integration

- [x] 3.1 Add `TokenUsageTracker tokenTracker` as a constructor parameter to `EnrichmentAgent`; store it as `private readonly TokenUsageTracker _tokenTracker`
- [x] 3.2 Inside the retry loop, add `_tokenTracker.EnforceBudget()` as the **first statement** of each attempt — before `ChatHistory` construction and before any SK call — so `BudgetExceededException` escapes the retry loop without being caught by the `JsonException` handler
- [x] 3.3 Immediately after the successful `GetChatMessageContentAsync` call, add: `var usage = response.Metadata?["Usage"] as Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIUsage; _tokenTracker.AddUsage(usage?.InputTokenCount ?? 0, usage?.OutputTokenCount ?? 0);`

## 4. SpecGeneratorAgent Integration

- [x] 4.1 Add `TokenUsageTracker tokenTracker` as a constructor parameter to `SpecGeneratorAgent`; store as `private readonly TokenUsageTracker _tokenTracker`
- [x] 4.2 Add `_tokenTracker.EnforceBudget()` as the first statement of each attempt inside the retry loop, before the SK call
- [x] 4.3 Immediately after the successful `GetChatMessageContentAsync` call, extract usage from `response.Metadata?["Usage"]` and call `_tokenTracker.AddUsage()` with the same null-safe pattern as in `EnrichmentAgent`

## 5. SpecCommand Integration

- [x] 5.1 Add `TokenUsageTracker tokenTracker` as a constructor parameter to `SpecCommand`; store as `private readonly TokenUsageTracker _tokenTracker`
- [x] 5.2 Declare `var budgetOption = new Option<decimal?>("--budget", "Monthly spend limit in USD (default: $20.00)")` in the command constructor and add it to the command; validate that the parsed value, if provided, is greater than zero (reject with an error message if not)
- [x] 5.3 At the start of the handler, before calling any agent: `if (budgetValue.HasValue) _tokenTracker.BudgetLimit = budgetValue.Value;`
- [x] 5.4 Add `catch (BudgetExceededException ex)` immediately before the generic `catch (Exception ex)` block; render a Spectre.Console `Panel` with title `"[red]Budget Exceeded[/]"` and body showing `$"Current cost: ${ex.CurrentCost:F2}"`, `$"Limit: ${ex.BudgetLimit:F2}"`, and the tip `"Tip: Reduce prompt size or increase --budget."`; set exit code to 1

## 6. Smoke Test

- [x] 6.1 Run `backlog-2-spec spec 99 --mock` and confirm the command completes with exit code 0 and no budget-related output — verifies the happy path is unaffected
- [x] 6.2 Temporarily set `BudgetLimit = 0m` in code (or pass `--budget 0.000001`), run `backlog-2-spec spec 99 --mock`, verify the "Budget Exceeded" panel renders with correct values, then revert the temporary change
