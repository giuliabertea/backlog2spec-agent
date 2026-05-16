## Why

The README and secrets configuration reference Azure OpenAI as a standalone resource, but Azure AI Foundry (formerly Azure AI Studio) is Microsoft's current unified AI platform — the one teams are more likely to have active today. Foundry supports both Azure OpenAI-based deployments and serverless API endpoints (Phi, Mistral, Llama, etc.), giving the tool model flexibility without locking users into a single provider.

## What Changes

- Rename config secret keys from `AzureOpenAI:*` to `AzureAI:*` to be provider-neutral **BREAKING**
- Update `KernelFactory` to support two endpoint modes: Azure OpenAI (classic resource URL) and Azure AI Foundry serverless inference (OpenAI-compatible URL)
- Add an optional `AzureAI:EndpointType` secret (`AzureOpenAI` | `AzureFoundry`) to select the connector mode; default to `AzureOpenAI` for backward compatibility
- Update user-secrets setup instructions and troubleshooting table in README.md
- Update the mock-mode error message and `--mock` help text to remove Azure OpenAI references

## Capabilities

### New Capabilities

- `azure-ai-connector`: A flexible AI connector layer that wraps Semantic Kernel registration, supporting both Azure OpenAI (classic) and Azure AI Foundry serverless endpoints via a single runtime-selected code path.

### Modified Capabilities

<!-- No existing behavioral specs change: enrichment-agent and spec-generator-agent retain the same inputs/outputs; only the underlying kernel wiring changes. -->

## Impact

- `src/Backlog2SpecAgent.Cli/Kernel/KernelFactory.cs` — add endpoint-type branching
- `src/Backlog2SpecAgent.Cli/Program.cs` — read renamed secrets and pass endpoint type to factory
- `README.md` — updated setup instructions for Azure AI Foundry (and kept Azure OpenAI as secondary option)
- `tests/Backlog2SpecAgent.Tests/` — no changes required (kernel is not directly tested; agents are tested via mocks)
