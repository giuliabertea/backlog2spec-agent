# Backlog2SpecAgent — Evolution to Foundry Agent Architecture

## Context
This is a .NET 8 CLI tool (`dotnet backlog-2-spec`) that:
1. Fetches an Azure DevOps work item
2. Enriches it (infers missing AC, edge cases, ambiguities) via a direct LLM call
3. Generates a structured spec (Goal / Behaviour / Edge Cases / Out of Scope / Files to Change)

Current architecture: CLI → direct Azure AI Foundry LLM call (no agent, no tools, no RAG).
The LLM is called as a plain function; all orchestration logic lives in C#.

## Goal
Evolve the tool in phases toward a true agentic architecture:
- Phase 1: Move prompt logic into a Foundry Agent (no behavior change, cleaner architecture)
- Phase 2: Expose .NET logic as HTTP tools callable by the agent
- Phase 3: Add RAG over the codebase (Azure AI Search)

## Phase 1 — Introduce a Foundry Agent (do this first)

Replace the current direct model call with a call to an Azure AI Foundry Agent.

### What to change

1. **Create `FoundryAgentClient.cs`** in `Backlog2SpecAgent.Cli/Infrastructure/AI/`
   - Use `Azure.AI.Projects` SDK (`AgentsClient`)
   - Constructor takes: `endpoint`, `apiKey`, `agentId` (from config/secrets)
   - Method: `Task<string> RunAsync(string userMessage)`
   - Creates a thread, sends message, runs the agent, polls until complete, returns the last assistant message text

2. **Update `AzureAiConfig`** (or equivalent config class) to add:
   - `AgentId` (string) — the Foundry agent ID
   - `UseAgent` (bool, default false) — feature flag to toggle between old and new path

3. **Update the DI registration** to conditionally inject `FoundryAgentClient` vs the existing `AzureAiClient` based on `UseAgent`

4. **Update secrets documentation** in README: add `AzureAI:AgentId` to the user-secrets setup section

5. **Keep mock mode working** — `UseAgent: false` in mock mode, or add a mock `IFoundryAgentClient`

### Agent system prompt (to be configured in Azure portal — NOT in code)
You are a senior software engineer generating production-ready structured specs
from Azure DevOps work items.
You receive a JSON object with:

workItem: the ADO ticket data
projectConfig: stack, conventions, architecture
devRules: (optional) team-specific architectural rules
repoContext: (optional) relevant source file snippets

Output ONLY a valid JSON object (no markdown, no prose) matching this schema:
{
"goal": "string",
"behaviour": ["string"],
"edgeCases": ["string"],
"outOfScope": ["string"],
"filesToChange": [{ "file": "string", "change": "string" }]
}
Rules:

Follow Clean Architecture principles
Respect devRules exactly — never suggest patterns listed as forbidden
Reference real file paths from repoContext when available
Be complete: cover all edge cases implied by the ticket


### What NOT to change in Phase 1
- The enrichment step (keep it as-is for now)
- The spec output rendering
- The `--mock` flag behavior
- The `--feature` / `--epic` flags

## Phase 2 — .NET Tools API (do NOT implement yet, only scaffold)

Create a new project `Backlog2SpecAgent.Tools` (ASP.NET Core minimal API) with these stubs:
- `GET /workitem/{id}` → calls existing ADO client, returns work item JSON
- `POST /repo-context` `{ "query": "string" }` → returns relevant repo file snippets
- `POST /spec` `{ "workItemId": int, "content": "string" }` → saves spec to disk

Add to solution but do not wire up to the agent yet. Just make sure it compiles and the endpoints return 501 Not Implemented for now.

## Phase 3 — RAG (do NOT implement yet)

Only add a `// TODO Phase 3: RAG` comment in `FoundryAgentClient.cs` where knowledge search would be triggered.

## Constraints
- Do not break existing CLI commands or flags
- Do not remove `--mock` support
- Maintain the existing `backlog-2-spec.json` config format (only additive changes)
- Use `Azure.AI.Projects` NuGet package for agent interaction
- All new async methods must be cancellable (`CancellationToken`)
- Follow the existing project conventions: constructor injection, `Result<T>` pattern if already used, no AutoMapper

## Deliverables
1. Modified source files (list them)
2. Updated README section for Phase 1 setup
3. The exact `dotnet user-secrets set` command for `AzureAI:AgentId`
4. A note on what to configure manually in Azure AI Foundry portal (agent creation is out of scope for code)