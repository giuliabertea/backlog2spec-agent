## Why

Backlog refinement is slow, manual, and inconsistent — engineers spend significant time turning vague work items into actionable specs. Backlog2SpecAgent Phase 1 introduces `backlog-2-spec`, a .NET 8 global CLI tool that fetches Azure DevOps work items, enriches them via Azure OpenAI, and renders structured Gherkin-style specs directly in the terminal — automating the most repetitive part of sprint planning.

## What Changes

- New .NET 8 global CLI tool (`backlog-2-spec`) packaged as a `dotnet tool`
- New command: `backlog-2-spec spec <id>` — fetch → enrich → generate → render pipeline
- New Azure DevOps integration via `VssBasicCredential` + `WorkItemHttpClient`
- New AI enrichment layer using Microsoft Semantic Kernel + Azure OpenAI
- New structured spec output: summary, Gherkin acceptance criteria, edge cases, out-of-scope, component breakdown
- New project config file (`backlog-2-spec.json`) for per-repo project/convention metadata
- New credential management via `dotnet user-secrets` (never in files or env vars)

## Capabilities

### New Capabilities

- `ado-client`: Fetches work items from Azure DevOps using PAT auth; maps Title, Description, AcceptanceCriteria, WorkItemType fields; strips HTML safely
- `enrichment-agent`: AI agent that analyzes a raw work item and produces structured enrichment — missing acceptance criteria, edge cases, constraints, affected components, ambiguities
- `spec-generator-agent`: AI agent that transforms an enriched ticket into a deterministic, structured `GeneratedSpec` with Gherkin-style acceptance criteria and stable section ordering
- `spec-command`: CLI entry point (`backlog-2-spec spec <id>`) that orchestrates the fetch → enrich → generate → render flow; supports `--verbose` and `--raw` flags; handles errors without crashing
- `output-renderer`: Sole class responsible for writing to the console via Spectre.Console; shows progress steps and renders specs with color-coded sections
- `config-loader`: Searches upward from CWD for `backlog-2-spec.json`; validates required fields; fails fast with descriptive errors

### Modified Capabilities

_(none — this is a greenfield implementation)_

## Impact

- **New solution**: `Backlog2SpecAgent/Backlog2SpecAgent.sln` with `src/Backlog2SpecAgent.Cli` and `tests/Backlog2SpecAgent.Tests`
- **Dependencies added**: `Microsoft.SemanticKernel`, `Microsoft.TeamFoundationServer.Client`, `System.CommandLine` (beta4), `Spectre.Console`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging`
- **Credentials**: `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, `AzureOpenAI:DeploymentName`, `Ado:Pat` — stored exclusively in `dotnet user-secrets`
- **Config file**: `backlog-2-spec.json` at repo root (or searched upward); defines project name, language, framework, ADO org/project, and conventions
- **No existing code affected** — pure greenfield
