# Backlog2SpecAgent

A CLI tool that turns an Azure DevOps work item into a ready-to-use, structured spec — in seconds.

Given a work item ID, it fetches the ticket from ADO, enriches it with AI (filling in missing acceptance criteria, edge cases, and ambiguities), optionally pulls relevant source files from your repository for grounding, then generates a structured spec tailored to your project's stack and conventions.

The output renders in the terminal with syntax highlighting, can be saved as a markdown file, or piped as JSON for automation.

---

## How it works

```
ADO work item
      │
      ▼
  Enrichment          ← infers missing AC, edge cases, ambiguities (direct LLM call)
      │
      ▼
  Spec generation     ← produces Goal / Behaviour / Edge Cases / Out of Scope / Files to Change
      │               ← uses: project config + dev rules + codebase context (ADO repo or RAG)
      ▼
  Terminal / .md / JSON
```

Three modes of operation — pick the one that fits your setup:

| Mode | What runs | When to use |
|---|---|---|
| **Direct** (default) | CLI → Azure OpenAI LLM | Simplest, works immediately |
| **Agent** (Phase 1) | CLI → Azure AI Foundry Agent | Iterate on the system prompt in the portal without code changes |
| **Agent + Tools** (Phase 2) | Agent calls `/workitem` and `/repo-context` HTTP tools at runtime | Full agentic flow; agent fetches its own data |

RAG (Phase 3) enriches the `repo-context` tool with a pre-built Azure AI Search index of your codebase, so the agent retrieves precise file snippets rather than guessing from ADO repo browsing.

---

## Quick start — deploy Azure resources

The ARM template in `infra/azuredeploy.json` provisions everything in one command:

- Azure AI Services account with GPT-4o deployment
- Azure AI Foundry hub + project (for agent mode)
- Azure AI Search service (for RAG)

### Option A — Azure CLI

```bash
# 1. Create a resource group (skip if you have one)
az group create --name b2s-rg --location eastus

# 2. Deploy all resources (~5 minutes)
az deployment group create \
  --resource-group b2s-rg \
  --template-file infra/azuredeploy.json \
  --parameters prefix=b2s location=eastus

# 3. Read the outputs — you will need these values in Step 6 (secrets)
az deployment group show \
  --resource-group b2s-rg \
  --name azuredeploy \
  --query properties.outputs
```

### Option B — PowerShell

```powershell
# 1. Create a resource group (skip if you have one)
New-AzResourceGroup -Name b2s-rg -Location eastus

# 2. Deploy all resources
New-AzResourceGroupDeployment `
  -ResourceGroupName b2s-rg `
  -TemplateFile infra/azuredeploy.json `
  -prefix b2s `
  -location eastus

# 3. Read the outputs
(Get-AzResourceGroupDeployment -ResourceGroupName b2s-rg -Name azuredeploy).Outputs
```

### Option C — Azure Portal

1. Go to [portal.azure.com](https://portal.azure.com) → **Deploy a custom template** → **Build your own template in the editor**.
2. Paste the contents of `infra/azuredeploy.json` and save.
3. Fill in `prefix` and `location`, then deploy.

> **GPT-4o regions:** The model is available in `eastus`, `eastus2`, `swedencentral`, `australiaeast`, `westus`, `westus3`. Use the same region for all resources to avoid cross-region egress.

> **Search SKU:** `free` is enough for small repos (≤ 50 MB, 1 per subscription). Use `basic` for a real codebase.

The outputs contain the exact values you will paste into `dotnet user-secrets` in Step 6.

---

## Step-by-step setup

Follow these steps in order. Steps 1–3 are one-time Azure setup. Steps 4–6 are one-time per machine. Steps 7–9 are one-time per project.

### Step 1 — Provision Azure resources

Use the ARM template above (recommended) **or** create the resources manually:

<details>
<summary>Manual Azure setup (click to expand)</summary>

#### 1a — Azure AI Foundry

1. Go to [ai.azure.com](https://ai.azure.com) and sign in.
2. Click **New project** → give it a name → let it create a hub and resource group.
3. Inside the project go to **Model catalog** → search `gpt-4o` → **Deploy**.
4. Deployment name: `gpt-4o`. Confirm.
5. Go to **Project → Settings → Keys and Endpoints** and note the **Endpoint URL** and **API Key**.

> Classic Azure OpenAI resource also works: use `https://your-resource.openai.azure.com` as the endpoint and omit `AzureAI:EndpointType` (or set it to `AzureOpenAI`).

#### 1b — Azure AI Search (for RAG — Phase 3)

1. In the [Azure portal](https://portal.azure.com) search for **Azure AI Search** → **Create**.
2. Choose **Basic** tier (or **Free** for small repos).
3. Same region as your AI Foundry project.
4. Once deployed, go to **Settings → Keys** and note the **Admin key** and the service URL (`https://<name>.search.windows.net`).

</details>

---

### Step 2 — Create the Foundry agent (Agent mode only)

> Skip this step if you are using **Direct mode** (the default). You can always add Agent mode later.

1. Go to [ai.azure.com](https://ai.azure.com) → your project → **Agents** → **New agent**.
2. Name it exactly `backlog2spec-agent` (the CLI resolves agents by name).
3. Paste this system prompt:

```
You are a senior software engineer generating production-ready structured specs
from Azure DevOps work items.

You receive a JSON object with:
  workItem:      the enriched ticket data (id, title, missingAcceptanceCriteria, edgeCases, constraints, affectedComponents, ambiguities)
  projectConfig: stack, conventions, architecture
  devRules:      (optional) team-specific architectural rules
  repoContext:   (optional) relevant source file snippets

Output ONLY a valid JSON object (no markdown, no prose) matching this schema:
{
  "goal": "string",
  "behaviour": ["string"],
  "edgeCases": ["string"],
  "outOfScope": ["string"],
  "filesToChange": [{ "file": "string", "change": "string" }]
}

Rules:
- Follow Clean Architecture principles
- Respect devRules exactly — never suggest patterns listed as forbidden
- Reference real file paths from repoContext when available
- Be complete: cover all edge cases implied by the ticket
```

4. Select the `gpt-4o` deployment. Save the agent.

> The tool registers the `get_work_item` and `repo_context` tool definitions on the agent automatically at first use — you do not need to add them manually in the portal.

---

### Step 3 — Create an Azure DevOps PAT

Each developer needs their own Personal Access Token.

1. Go to `https://dev.azure.com/{your-org}/_usersSettings/tokens` → **New Token**.
2. Configure:

   | Setting | Value |
   |---|---|
   | Name | `Backlog2SpecAgent` |
   | Expiration | 1 year (set a calendar reminder to renew) |
   | Work Items | **Read** |
   | Code | **Read** — only required if you set `repoName` in the config file |

3. Copy the token — you will not see it again.

---

### Step 4 — Clone this repo and install the CLI tool

```bash
git clone https://github.com/giuliabertea/Backlog2SpecAgent
cd Backlog2SpecAgent

dotnet pack src/Backlog2SpecAgent.Cli -o ./nupkg
dotnet tool install --global --add-source ./nupkg Backlog2SpecAgent.Cli
```

Verify:

```bash
backlog-2-spec-agent --version
```

> **Local install (per-project):** If you prefer `--local` instead of `--global`, first run `dotnet new tool-manifest` inside **your** project directory, then come back here and run the install with `--local`.

---

### Step 5 — Add the config file to your project

Create `backlog-2-spec.json` in the root of **your project** (not the Backlog2SpecAgent repo). The tool searches upward from the current working directory to find it.

```json
{
  "project": {
    "name": "MyService",
    "language": "C#",
    "framework": ".NET 8 / ASP.NET Core",
    "testFramework": "xUnit",
    "architecture": "Clean Architecture"
  },
  "conventions": {
    "naming": "PascalCase classes, camelCase fields",
    "folderStructure": "Feature-based",
    "diPattern": "Constructor injection"
  },
  "ado": {
    "organization": "https://dev.azure.com/your-org",
    "project": "YourProject",
    "repoName": "YourRepo",
    "branch": "main"
  },
  "devRulesFile": "dev-rules.md"
}
```

Required fields: `ado.organization`, `ado.project`, `project.name`. The `repoName` and `branch` fields are optional — when set, the tool fetches relevant source files from your ADO repo and uses them as context. The `devRulesFile` field is optional — see the [Project rules file](#project-rules-file) section.

**Commit this file to your project repo.** It contains no secrets.

```bash
git add backlog-2-spec.json
git commit -m "add backlog-2-spec config"
```

---

### Step 6 — Set secrets

Credentials are stored via `dotnet user-secrets` — never in files. Each developer runs this on their own machine. Run from inside the **Backlog2SpecAgent repo**:

```bash
cd path/to/Backlog2SpecAgent/src/Backlog2SpecAgent.Cli
```

#### Direct mode (default)

```bash
dotnet user-secrets set "AzureAI:Endpoint"       "https://<name>.cognitiveservices.azure.com/"
dotnet user-secrets set "AzureAI:ApiKey"         "your-api-key"
dotnet user-secrets set "AzureAI:DeploymentName" "gpt-4o"
dotnet user-secrets set "AzureAI:EndpointType"   "AzureFoundry"

dotnet user-secrets set "Ado:Pat"                "your-ado-pat"
```

> If you used the ARM template, copy the `aiServicesEndpoint`, `aiServicesKey`, and `gptDeploymentName` values from the deployment outputs directly.

#### Agent mode (add these on top of Direct mode secrets)

```bash
dotnet user-secrets set "AzureAI:AgentName" "backlog2spec-agent"
dotnet user-secrets set "AzureAI:UseAgent"  "true"
```

#### Tools API (add these if deploying Phase 2)

```bash
dotnet user-secrets set "AzureAI:ToolsBaseUrl" "https://your-tools-api.azurewebsites.net"
dotnet user-secrets set "AzureAI:ToolsApiKey"  "your-shared-secret"
```

> **Migrating from a previous version?** The secret keys were renamed: `AzureOpenAI:*` → `AzureAI:*`. Re-run the commands above with the new names.

---

### Step 7 — Index your codebase for RAG (Phase 3)

This step builds the Azure AI Search index that lets the agent retrieve grounded code snippets instead of guessing. Run it once after setup, and again whenever your codebase changes significantly.

```powershell
# From the Backlog2SpecAgent repo root
.\scripts\index-repo.ps1 `
    -SearchUrl  "https://<your-search>.search.windows.net" `
    -SearchKey  "<admin-key>" `
    -RepoPath   "C:\path\to\your-project"
```

> If you used the ARM template, copy `-SearchUrl` from the `searchEndpoint` output and `-SearchKey` from `searchAdminKey`.

The script:
- Recursively scans `.cs` and `.md` files; skips `bin/`, `obj/`, and `*.generated.cs`
- Splits files into 300–500 line chunks at class/method and heading boundaries
- Upserts chunks to an index named `codebase-chunks` by default (created automatically on first run; override with `-IndexName`)
- Is safe to re-run — existing documents are updated, not duplicated

Typical run on a 50-file project takes under 30 seconds.

---

### Step 8 — Deploy the Tools API (Phase 2 only)

> Skip this step if you are using Direct mode or Agent mode without tools.

The Tools API is a self-contained ASP.NET Core app that the agent calls at runtime. Build it with Docker:

```bash
# From the repo root
docker build -f src/Backlog2SpecAgent.Tools/Dockerfile . -t b2s-tools

docker run -d -p 8080:8080 \
  -e Ado__Organization="https://dev.azure.com/your-org" \
  -e Ado__Project="YourProject" \
  -e Ado__RepoName="YourRepo" \
  -e Ado__Branch="main" \
  -e Ado__Pat="your-ado-pat" \
  -e Security__ApiKey="your-shared-secret" \
  b2s-tools
```

Or run locally without Docker:

```bash
cd src/Backlog2SpecAgent.Tools
dotnet run
```

The API must be reachable from Azure AI Foundry (i.e., publicly accessible or on the same VNet). Update the agent system prompt to instruct it to use tools (see the [Agent + Tools system prompt](#agent--tools-system-prompt) section).

---

### Step 9 — Verify

Run a mock smoke test from inside **your project directory** (no Azure calls, no secrets required):

```bash
cd path/to/your-project
backlog-2-spec-agent spec 1 --mock
```

This runs the full pipeline with stub implementations. If it prints a spec, your config file is found and parsed correctly.

For a live test:

```bash
backlog-2-spec-agent spec 12345
```

---

## Usage

### Basic

```bash
backlog-2-spec-agent spec 12345
```

### With verbose enrichment detail

```bash
backlog-2-spec-agent spec 12345 --verbose
```

Shows the AI-identified missing acceptance criteria, edge cases, and ambiguities before the spec.

### Save to markdown

```bash
backlog-2-spec-agent spec 12345 --output ./specs/feature-12345.md
```

### JSON output (pipe-friendly)

```bash
backlog-2-spec-agent spec 12345 --raw
backlog-2-spec-agent spec 12345 --raw | jq .goal
```

### Dry run without external calls

```bash
backlog-2-spec-agent spec 12345 --mock
```

### Export all specs for a Feature or Epic

```bash
backlog-2-spec-agent spec 12345 --feature
backlog-2-spec-agent spec 12345 --epic
```

Fetches the parent work item and all its children, generates a spec for each, and writes them to `spec/<id>-<slug>/`. A `_summary.md` index file is also created.

`--feature` and `--epic` are mutually exclusive. If the work item type does not match, the tool reports an error.

---

## Spec output format

Each generated spec contains five sections:

| Section | Description |
|---|---|
| **Goal** | 1–3 sentences. The first states the capability being built; the others add outcome or non-obvious constraints. |
| **Behaviour** | Plain-English bullets describing what the implementation must do. Written from the developer's perspective. |
| **Edge Cases** | Boundary conditions and failure scenarios the developer should handle. |
| **Out of Scope** | Things explicitly excluded from this work item. |
| **Files to Change** | File paths with a one-line description of what changes in each. Resolved from actual source files when codebase context is available. |

Example output:

```
── Goal ─────────────────────────────────────────
Add rate limiting to the login endpoint so accounts lock after 5 failed attempts.
The lockout state is persisted per user and resets on successful login.

── Behaviour ────────────────────────────────────
  • Increment a failed attempt counter on each wrong password
  • Lock the account after 5 consecutive failures
  • Return 423 with a lockout message when the account is locked
  • Reset the counter on successful login
  • Normalize email to lowercase before lookup

── Edge Cases ───────────────────────────────────
  ⚠ Mixed-case email must match existing accounts
  ⚠ Concurrent login attempts must not bypass the counter

── Out of Scope ─────────────────────────────────
SSO, session timeout, password reset

── Files to Change ──────────────────────────────
  • src/Services/AuthService.cs: add EnforceLockout() and ResetAttempts()
  • src/Repositories/UserRepository.cs: add UpdateFailedAttempts()
  • src/Controllers/LoginController.cs: return 423 on locked account
```

---

## Using specs with AI coding assistants

The spec output format is designed to be pasted directly into GitHub Copilot Chat, Cursor, or any similar AI coding assistant.

### Single PBI

Save the spec with `--output`, then attach it in Copilot Chat and use:

```
Implement the spec in the attached file.
Follow the "Files to Change" section exactly — only touch the listed files.
Do not implement anything listed under "Out of Scope".
```

### Full feature (multiple PBIs)

When you export a feature with `--feature` or `--epic`, a folder of spec files is created under `spec/<id>-<slug>/`. Use this prompt, replacing `<folder>`:

```
Implement the following feature step by step.

The feature spec is in `spec/<folder>/00-feature.md`.
Each PBI spec is a numbered file in the same folder.

Rules:
1. Read the feature spec first to understand the overall goal and scope.
2. Implement each PBI in file order (01, 02, …), one at a time.
3. For each PBI, follow the "Files to Change" section exactly — only touch the listed files.
4. Respect the "Out of Scope" section: do not implement anything listed there.
5. After each PBI, stop and summarise what you changed before moving to the next.
6. Do not refactor or improve code outside of what the spec asks for.

Start with the feature spec, confirm your understanding of the goal, then begin PBI 01.
```

> **Tip:** In Copilot Workspace you can attach all spec files directly — replace file path references with *"use the attached spec files"* and omit the folder reference.

---

## Project rules file

The `devRulesFile` field in `backlog-2-spec.json` points to a markdown file that gets injected verbatim into both the enrichment and spec generation prompts.

Use it to encode team-specific constraints the AI should always respect — things not expressible through the structured config fields.

### Good candidates

- Architectural constraints: *"never put business logic in controllers"*
- Patterns that must be followed: *"always use the Result<T> pattern, never throw exceptions from services"*
- Things to avoid: *"do not use AutoMapper — map manually"*
- Layer ownership: *"the domain layer must not depend on infrastructure"*
- Naming conventions too nuanced for a one-liner

### Setup

1. Create the file in your project root:

   ```markdown
   # dev-rules.md

   - Never put business logic in controllers.
   - All service methods return Result<T>. Never throw exceptions from the service layer.
   - Do not use AutoMapper. All mapping is done in dedicated mapper classes.
   - The domain layer must not reference any infrastructure or application layer types.
   - Repository interfaces live in the domain layer; implementations in the infrastructure layer.
   - Commands and queries follow MediatR: VerbNounCommand / VerbNounCommandHandler.
   ```

2. Reference it in `backlog-2-spec.json`:

   ```json
   { "devRulesFile": "dev-rules.md" }
   ```

3. Commit both files.

---

## Agent + Tools system prompt

When deploying Phase 2 (the Tools API), update the agent system prompt in [ai.azure.com](https://ai.azure.com) to instruct the agent to call tools rather than read from a pre-built payload:

```
You are a senior software engineer generating production-ready structured specs
from Azure DevOps work items.

You receive a JSON object with a single field: { "workItemId": <int> }.

Steps:
1. Call get_work_item with the workItemId to fetch the ticket details.
2. Call repo_context with a relevant search query to fetch related source files.
3. Using the ticket data and source context, generate a structured spec.

Output ONLY a valid JSON object (no markdown, no prose) matching this schema:
{
  "goal": "string",
  "behaviour": ["string"],
  "edgeCases": ["string"],
  "outOfScope": ["string"],
  "filesToChange": [{ "file": "string", "change": "string" }]
}

Rules:
- Follow Clean Architecture principles
- Reference real file paths from the repo context when available
- Be complete: cover all edge cases implied by the ticket
```

> The tool definitions are registered on the agent automatically by the CLI on first use — you do not need to add them in the portal.

---

## Mock mode

```bash
backlog-2-spec-agent spec 12345 --mock
```

Mock mode replaces every external dependency — ADO client, enrichment agent, spec generator — with fast stub implementations that return fixed data. No credentials are required and no network calls are made.

Use it to:
- Verify your `backlog-2-spec.json` config is found and parsed correctly
- Test output formatting without waiting for AI responses
- Try the full pipeline in CI or on a machine without secrets configured

Mock mode is detected at startup (before the DI container is built), so it works even if `AzureAI:*` secrets are not set.

---

## Keeping the tool up to date

```bash
cd path/to/Backlog2SpecAgent
git pull
dotnet pack src/Backlog2SpecAgent.Cli -o ./nupkg
dotnet tool update --global --add-source ./nupkg Backlog2SpecAgent.Cli
```

Use `--local` instead of `--global` if you installed locally. Re-run `scripts/index-repo.ps1` after a pull if the RAG index format has changed.

---

## Troubleshooting

| Error | Cause | Fix |
|---|---|---|
| `Configuration error: 'backlog-2-spec.json' not found` | No config file in CWD or any parent | Create `backlog-2-spec.json` in your project root |
| `Missing required field: ado.organization` | Config file incomplete | Add the missing field |
| `Configuration error: devRulesFile not found: '...'` | Path in `devRulesFile` does not exist | Check the path is relative to `backlog-2-spec.json` |
| `Authentication error: Failed to connect to Azure DevOps` | Invalid PAT or org URL | Re-set `Ado:Pat` and verify `ado.organization` |
| `Authentication error: Authentication failed` | PAT expired or wrong scope | Generate a new PAT with Work Items: Read |
| `AI response error: LLM returned invalid JSON` | Model returned malformed JSON after 3 retries | Check deployment name and quota; try again |
| `Unexpected error: AzureAI:Endpoint secret is missing` | User secrets not set | Run the secrets setup commands in Step 6 |
| `No manifest file found` | Missing `.config/dotnet-tools.json` | Run `dotnet new tool-manifest` in your project root |
| `Unexpected error: AzureAI:AgentName secret is missing` | `UseAgent` is true but `AgentName` not set | `dotnet user-secrets set "AzureAI:AgentName" "backlog2spec-agent"` |
| `Agent 'backlog2spec-agent' not found` | Agent name mismatch | Check the agent name in [ai.azure.com](https://ai.azure.com) — it is case-sensitive |
| `index-repo.ps1` fails with 401 | Wrong search key | Use the **admin key** from Azure AI Search → Settings → Keys |
| `index-repo.ps1` fails with 404 on index creation | Search service not found | Check `-SearchUrl` matches your service name exactly |
| GPT-4o deployment fails with `ModelNotFound` | Model not available in the selected region | Redeploy to `eastus`, `swedencentral`, or `australiaeast` |
