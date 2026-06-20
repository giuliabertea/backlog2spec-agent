## Why

Today `FoundrySpecGeneratorAgent` uses the LLM as a **template compiler**: it
pre-fetches five code snippets from Azure AI Search using only the work item
title as a query, packs everything into a single payload, and fires one call to
the Foundry agent. The agent never sees the repo — it reasons on pre-selected
context assembled in C#.

The result is that `filesToChange` is built on guesswork: the agent cannot read
around a candidate, follow a caller chain, or verify that a file actually
contains the logic it claims to modify. In practice this produces path
hallucinations and vague change descriptions that give GitHub Copilot a weak
starting point.

The fix is to invert the flow: let the **agent** decide what to read and when.
The Tools API is already an HTTP service deployed on App Service. By exposing it
as an OpenAPI tool on the Foundry agent we give the agent fine-grained
navigation tools (read file, list directory, find references, get outline) and
replace the C#-side one-shot retrieval with an **agentic investigation loop**
guided by an explicit senior-engineer protocol.

## What Changes

- **Tools API (`src/Backlog2SpecAgent.Tools/Program.cs`)**: add four navigation
  endpoints (`GET /repo/tree`, `GET /repo/file`, `POST /repo/references`,
  `GET /repo/outline`) and expose the full service as an OpenAPI 3.0 document so
  Foundry can import it as an OpenAPI tool. Assign explicit `operationId` values
  and rich descriptions to every endpoint so the agent can choose the right tool
  from its description alone.

- **Spec schema (`GeneratedSpec` + `FoundrySpec`)**: extend `filesToChange` with
  `evidence` (what the agent actually read) and `confidence` (`high` / `medium` /
  `low`). Add `openQuestions` and `conventions` as top-level arrays. This makes
  grounding machine-verifiable.

- **Spec generator (`FoundrySpecGeneratorAgent`)**: remove the C#-side Azure AI
  Search call and the pre-packaged `repoContext` field from the payload. The
  agent now receives only `{ workItem, projectConfig, devRules }` and retrieves
  codebase context autonomously via its OpenAPI tool during the run. The existing
  `FoundryAgentClient` poll-until-complete loop already handles multi-step runs
  transparently — no changes required there.

- **Output renderer (`OutputRenderer`)**: render the new schema fields — evidence
  and confidence badge per file, and separate sections for Open Questions and
  Conventions.

- **Agent configuration (Foundry portal)**: replace the current system prompt with
  a five-step investigation protocol (Understand → Hypothesise → Investigate →
  Impact → Plan) that mandates tool use before any path can appear in the output.
  Optionally upgrade the deployment to a reasoning model.

## Capabilities

### New Capabilities

- `repo-navigation-tools`: Four new HTTP endpoints on the Tools API, exposed as
  OpenAPI operations, that give the Foundry agent the ability to navigate the
  codebase at read time: list a directory, read a file by line range, find all
  references to a symbol, and get a structural outline of a source file.

- `openapi-tool-registration`: The Tools API exposes a compliant
  `/swagger/v1/swagger.json` document with `operationId`, `summary`, and rich
  `description` on every operation. This document is the artifact imported into
  Foundry as an OpenAPI tool.

- `grounded-spec-schema`: The `filesToChange` array now carries `evidence` and
  `confidence` per entry. No path may appear in the output without citing
  something the agent actually read. Ambiguities and unverified locations go to
  `openQuestions`.

### Modified Capabilities

- `spec-generator-agent`: `FoundrySpecGeneratorAgent.GenerateAsync` sends a
  leaner payload (no `repoContext`), and maps the enriched response schema.

- `output-renderer`: `OutputRenderer.RenderSpec` and `RenderMarkdown` render the
  extended schema fields.

## Impact

- `src/Backlog2SpecAgent.Tools/Program.cs` — new endpoints + Swashbuckle setup
- `src/Backlog2SpecAgent.Tools/Backlog2SpecAgent.Tools.csproj` — add
  `Swashbuckle.AspNetCore`
- `src/Backlog2SpecAgent.Cli/Models/GeneratedSpec.cs` — extended schema
- `src/Backlog2SpecAgent.Cli/Agents/FoundrySpecGeneratorAgent.cs` — leaner
  payload, updated internal `FoundrySpec`/`FoundryFileChange` classes, updated
  `MapToGeneratedSpec`
- `src/Backlog2SpecAgent.Cli/Output/OutputRenderer.cs` — render new fields
- `tests/Backlog2SpecAgent.Tests/SpecGeneratorAgentTests.cs` — update for new
  schema
- `tests/Backlog2SpecAgent.Tests/OutputRendererTests.cs` — update for new render
- `README.md` — document new secrets and portal setup steps
- **Foundry portal** (out of code scope): register OpenAPI tool, replace system
  prompt, optionally upgrade model deployment
