## Context

`KernelFactory.cs` currently calls `AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey)` unconditionally. This method expects a classic Azure OpenAI resource endpoint (`https://<name>.openai.azure.com`). Azure AI Foundry serverless inference endpoints have a different URL shape (`https://<endpoint>.<region>.inference.ai.azure.com`) and are registered via `AddOpenAIChatCompletion` (the OpenAI-compatible variant) in Semantic Kernel.

Config keys are currently named `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, `AzureOpenAI:DeploymentName` — all set via `dotnet user-secrets`. These names are misleading once the underlying provider can be Azure AI Foundry.

## Goals / Non-Goals

**Goals:**
- Support Azure AI Foundry serverless inference endpoints alongside classic Azure OpenAI resources
- Make the endpoint type explicit via a config key so users know exactly what mode the tool is running in
- Rename secrets to provider-neutral names (`AzureAI:*`)
- Keep the change backward-compatible via a default (old Azure OpenAI behavior stays the default)

**Non-Goals:**
- Supporting non-OpenAI-compatible model APIs (e.g., raw Hugging Face endpoints)
- Switching the AI framework away from Semantic Kernel
- Adding model-specific prompt tuning for non-GPT models
- Supporting multiple simultaneous providers

## Decisions

### D1: Add `AzureAI:EndpointType` secret to branch behavior

Options considered:
- **Auto-detect by URL shape** — fragile; URL formats could overlap or change.
- **Separate config profiles** — cleaner long-term but over-engineered for a CLI tool.
- **Explicit `EndpointType` key (chosen)** — simple, explicit, fails fast with a clear message. Accepted values: `AzureOpenAI` (default) or `AzureFoundry`.

### D2: Rename secrets from `AzureOpenAI:*` to `AzureAI:*`

This is a breaking change for existing users (they must re-set their secrets). Acceptable because:
- The tool is pre-1.0 with no published consumers
- The old names are actively misleading once Foundry is supported

The `Program.cs` error messages will reference the new key names, so the failure is obvious.

### D3: Keep `KernelFactory.Build` signature — add overload for endpoint type

`KernelFactory.Build(endpoint, apiKey, deploymentName)` gains an optional `endpointType` parameter (enum: `AzureOpenAI` | `AzureFoundry`). Inside, a `switch` branches to either `AddAzureOpenAIChatCompletion` or `AddOpenAIChatCompletion`. This keeps callers simple and the factory testable.

```csharp
// AzureFoundry branch
builder.AddOpenAIChatCompletion(
    modelId: deploymentName,
    apiKey: apiKey,
    endpoint: new Uri(endpoint));
```

## Risks / Trade-offs

- **Breaking secret rename** → Mitigation: clear error message with exact new key name when secrets are missing; README migration section shows old → new mapping.
- **Model-specific prompt quality** → Foundry non-GPT models (Phi, Mistral) may produce worse JSON output than GPT-4o. Mitigation: document that GPT-4o or equivalent is recommended; the existing retry logic (`LlmFormatException`) already handles malformed JSON.
- **Semantic Kernel version drift** → `AddOpenAIChatCompletion` with a custom URI is available in SK 1.x (current: 1.75.0). No version bump needed.

## Migration Plan

1. Users must re-set secrets after pulling the new version:
   ```bash
   dotnet user-secrets set "AzureAI:Endpoint"       "<url>"
   dotnet user-secrets set "AzureAI:ApiKey"          "<key>"
   dotnet user-secrets set "AzureAI:DeploymentName"  "<name>"
   dotnet user-secrets set "AzureAI:EndpointType"    "AzureFoundry"   # omit to keep old behavior
   ```
2. Rollback: revert the `Program.cs` secret key reads and `KernelFactory` changes; no data or schema changes to undo.

## Open Questions

- Should `EndpointType` default to `AzureOpenAI` (safe, backward-compatible) or `AzureFoundry` (forward-looking)? Current decision: default to `AzureOpenAI` for backward compatibility.
- Do we want to add a `--list-models` command to verify connectivity to the configured endpoint? Out of scope for now.
