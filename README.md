# Backlog2SpecAgent

A CLI tool that turns an Azure DevOps work item into a ready-to-use, structured spec — in seconds.

Given a work item ID, it fetches the ticket from ADO, enriches it with AI (filling in missing acceptance criteria, edge cases, and ambiguities), optionally pulls relevant source files from your repository for grounding, then generates a structured spec tailored to your project's stack and conventions.

The output renders in the terminal with syntax highlighting, can be saved as a markdown file, or piped as JSON for automation.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- An Azure DevOps account with access to your project
- An AI endpoint — either an [Azure AI Foundry](https://ai.azure.com) project with a deployed model, or a classic Azure OpenAI resource

---

## Setup

Follow these steps in order. Steps 1–2 are one-time Azure setup. Steps 3–5 are one-time per machine. Steps 6–7 are one-time per project and should be committed to your project repo.

### Step 1 — Create an Azure AI Foundry project

> **Team note:** You can create one shared project for the whole team (simpler — one endpoint and API key to distribute) or each developer can create their own (fully isolated quota). Either approach works; the secrets setup in Step 5 is identical either way.

1. Go to [ai.azure.com](https://ai.azure.com) and sign in with your Azure account.
2. Click **New project**, give it a name, and let it create a hub and resource group.
3. Inside the project, go to **Model catalog** → search for `gpt-4o` → **Deploy**.
4. Give the deployment a name (e.g. `gpt-4o`) and confirm.
5. Once deployed, go to **Project → Settings → Keys and Endpoints** and note:
   - **Endpoint URL** — looks like `https://<name>.<region>.inference.ai.azure.com`
   - **API Key**
   - **Deployment name** — whatever you used in step 4

You will need these three values in Step 5.

> If you are using a **classic Azure OpenAI resource** instead, use the endpoint from your Azure OpenAI resource (format: `https://your-resource.openai.azure.com`) and omit `AzureAI:EndpointType` (or set it to `AzureOpenAI`).

---

### Step 2 — Create an Azure DevOps PAT

Each developer needs their own Personal Access Token.

1. Go to `https://dev.azure.com/{your-org}/_usersSettings/tokens` → **New Token**.
2. Configure it:

   | Setting | Value |
   |---|---|
   | Name | `Backlog2SpecAgent` |
   | Expiration | 1 year (set a calendar reminder to renew) |
   | Work Items | **Read** |
   | Code | **Read** — only required if you set `repoName` in the config file |

3. Copy the token — you will not see it again.

---

### Step 3 — Clone this repo and install the tool

```bash
git clone https://github.com/giuliabertea/Backlog2SpecAgent
cd Backlog2SpecAgent

dotnet pack src/Backlog2SpecAgent.Cli -o ./nupkg
dotnet tool install --local --add-source ./nupkg Backlog2SpecAgent.Cli
```

The `--local` flag installs the tool into your current project's tool manifest (`.config/dotnet-tools.json`). If your project does not have one yet, create it first:

```bash
# Run this inside YOUR project directory, not the Backlog2SpecAgent repo
dotnet new tool-manifest
```

Then go back to the Backlog2SpecAgent repo and run the `dotnet pack` / `dotnet tool install` commands above.

Alternatively, install globally to avoid the manifest requirement:

```bash
dotnet tool install --global --add-source ./nupkg Backlog2SpecAgent.Cli
```

Verify the installation:

```bash
dotnet backlog-2-spec --version
```

---

### Step 4 — Add the config file to your project

Create `backlog-2-spec.json` in the root of **your project** (not the Backlog2SpecAgent repo). The tool searches upward from the current directory to find it.

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

Required fields: `ado.organization`, `ado.project`, `project.name`. The `repoName` and `branch` fields are optional — when set, the tool fetches relevant source files from your repo and feeds them as context to the AI. The `devRulesFile` field is optional — see the section below.

**Commit this file to your project repo.** It contains no secrets (only org name and project name). Committing it means every developer who clones your project gets the config automatically.

```bash
git add backlog-2-spec.json
git commit -m "add backlog-2-spec config"
```

---

### Step 5 — Set secrets

Credentials are stored via `dotnet user-secrets` — never in files. **Each developer must run this on their own machine.** Secrets are stored by the OS in a per-user location and are never shared or committed.

Run these commands from inside the **Backlog2SpecAgent repo**:

```bash
cd path/to/Backlog2SpecAgent/src/Backlog2SpecAgent.Cli

# Azure AI Foundry (recommended)
dotnet user-secrets set "AzureAI:Endpoint"       "https://<name>.<region>.inference.ai.azure.com"
dotnet user-secrets set "AzureAI:ApiKey"         "your-api-key"
dotnet user-secrets set "AzureAI:DeploymentName" "gpt-4o"
dotnet user-secrets set "AzureAI:EndpointType"   "AzureFoundry"

# Classic Azure OpenAI resource (omit AzureAI:EndpointType or set it to "AzureOpenAI")
dotnet user-secrets set "AzureAI:Endpoint"       "https://your-resource.openai.azure.com"
dotnet user-secrets set "AzureAI:ApiKey"         "your-api-key"
dotnet user-secrets set "AzureAI:DeploymentName" "gpt-4o"

# Azure DevOps PAT (required regardless of endpoint type)
dotnet user-secrets set "Ado:Pat"                "your-ado-pat"
```

> **Migrating from a previous version?** The secret keys were renamed:
> `AzureOpenAI:Endpoint` → `AzureAI:Endpoint`, `AzureOpenAI:ApiKey` → `AzureAI:ApiKey`, `AzureOpenAI:DeploymentName` → `AzureAI:DeploymentName`.
> Re-run the `dotnet user-secrets set` commands above with the new names.

---

### Step 6 — Verify the setup

Before making any real ADO or AI calls, run a mock smoke test from inside **your project directory**:

```bash
cd path/to/your-project
dotnet backlog-2-spec spec 1 --mock
```

This runs the full pipeline with no external calls. If it prints a spec, your config file is found and parsed correctly. Proceed to real usage only after this passes.

---

## Usage

### Basic

```bash
dotnet backlog-2-spec spec 12345
```

Fetches work item #12345, enriches it, and prints the spec to the terminal.

### With verbose enrichment detail

```bash
dotnet backlog-2-spec spec 12345 --verbose
```

Shows the AI-identified missing acceptance criteria, edge cases, and ambiguities before the spec.

### Save to markdown

```bash
dotnet backlog-2-spec spec 12345 --output ./specs/feature-12345.md
```

### JSON output (pipe-friendly)

```bash
dotnet backlog-2-spec spec 12345 --raw
dotnet backlog-2-spec spec 12345 --raw | jq .goal
```

### Dry run without external calls

```bash
dotnet backlog-2-spec spec 12345 --mock
```

Runs the full pipeline with mock implementations — no ADO or AI calls. Useful for testing config and output formatting.

### Export all specs for a Feature or Epic

```bash
dotnet backlog-2-spec spec 12345 --feature
dotnet backlog-2-spec spec 12345 --epic
```

Fetches the parent work item and all its children, generates a spec for each child, and writes them to a folder under `spec/<id>-<slug>/`. A `_summary.md` index file is also created with a table linking to each child spec.

`--feature` and `--epic` are mutually exclusive. If the work item type does not match the flag used, the tool reports an error.

---

## Spec output format

Each generated spec contains five sections:

| Section | Description |
|---|---|
| **Goal** | 1–3 sentences. The first states the capability being built; the others (optional) add outcome or non-obvious constraints. |
| **Behaviour** | Plain-English bullets describing what the implementation must do, one per distinct behaviour. Written from the developer's perspective. |
| **Edge Cases** | Boundary conditions and failure scenarios the developer should handle. |
| **Out of Scope** | Comma-separated list of things explicitly excluded from this work item. |
| **Files to Change** | File paths with a one-line description of what changes in each. Resolved from actual source files when codebase context is available. |

Example output (console):

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

Save the spec to a file with `--output`, then attach it in Copilot Chat and use:

```
Implement the spec in the attached file.
Follow the "Files to Change" section exactly — only touch the listed files.
Do not implement anything listed under "Out of Scope".
```

Or paste the spec content inline and prepend the same instruction.

### Full feature (multiple PBIs)

When you export a feature with `--feature` or `--epic`, a folder of spec files is created under `spec/<id>-<slug>/`. Give Copilot this prompt, replacing `<folder>` with the actual folder name:

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

> **Tip:** In Copilot Workspace you can attach all spec files directly — replace the file path references with *"use the attached spec files"* and omit the folder reference.

---

## Project rules file

The `devRulesFile` field in `backlog-2-spec.json` points to a markdown file that gets injected verbatim as a **Development Rules** section into both the enrichment and spec generation prompts.

Use it to encode team-specific constraints that the AI should always respect when analysing tickets and writing specs — things that are not expressible through the structured config fields.

### When to use it

Good candidates for the rules file:

- Architectural constraints ("never put business logic in controllers")
- Patterns that must be followed ("always use the Result<T> pattern, never throw exceptions from services")
- Things to avoid ("do not use AutoMapper — map manually")
- Layer ownership ("the domain layer must not depend on infrastructure")
- Naming conventions too nuanced for a one-liner ("commands are named VerbNounCommand, handlers are VerbNounCommandHandler")

### How to set it up

1. Create the rules file in your project root (or anywhere — the path is resolved relative to `backlog-2-spec.json`):

   ```markdown
   # dev-rules.md

   - Never put business logic in controllers. Controllers only validate input, call a service, and return a response.
   - All service methods return Result<T>. Never throw exceptions from the service layer.
   - Do not use AutoMapper. All object mapping is done manually in dedicated mapper classes.
   - The domain layer must not reference any infrastructure or application layer types.
   - Repository interfaces live in the domain layer; implementations live in the infrastructure layer.
   - Commands and queries follow the MediatR pattern: VerbNounCommand / VerbNounQuery, handled by VerbNounCommandHandler / VerbNounQueryHandler.
   ```

2. Reference it in `backlog-2-spec.json`:

   ```json
   {
     "devRulesFile": "dev-rules.md"
   }
   ```

   The path is relative to the config file. You can also use an absolute path or a subdirectory:

   ```json
   {
     "devRulesFile": "docs/Backlog2SpecAgent-rules.md"
   }
   ```

3. Commit both files:

   ```bash
   git add backlog-2-spec.json dev-rules.md
   git commit -m "add Backlog2SpecAgent project rules"
   ```

### How it affects output

When the rules file is set, both AI steps include a `## Development Rules` section built from its content:

- **Enrichment** — the AI uses the rules when inferring affected components and identifying missing acceptance criteria, so it won't suggest components that violate your layering rules
- **Spec generation** — the AI uses the rules when deciding which files to touch and how to describe the behaviour, so specs won't suggest patterns your team has explicitly ruled out

If `devRulesFile` is not set, both steps run without any injected rules — the structured config fields (`architecture`, `naming`, etc.) still apply.

---

## What you gain

**Specs in minutes, not hours.** Writing a complete structured spec from scratch for a mid-size ticket easily takes 30–60 minutes. Backlog2SpecAgent does it in under a minute, with a result that already matches your project's naming conventions, test framework, and architecture.

**Catches what tickets miss.** Most backlog items skip edge cases, have underspecified acceptance criteria, or leave ambiguities implicit. The enrichment step surfaces these explicitly — so the spec you get is already more thorough than what the ticket contained.

**Grounded in your actual codebase.** When `repoName` is configured, the tool fetches files relevant to the ticket from your ADO repository and includes them as context. The generated spec references real file paths, existing class names, and the correct layer boundaries — not generic placeholders.

**Optimised for AI coding assistants.** The output format (Goal + Behaviour + Files to Change) is designed to be pasted directly into GitHub Copilot or similar tools. Plain-English bullets and concrete file paths give the assistant high-signal, low-token context — more actionable than a BDD test spec.

**Consistent across the team.** Every spec produced by the tool follows the same structure and style, regardless of who runs it. Pair it with the `devRulesFile` to encode your team's architectural decisions and have them applied automatically to every spec.

**Scales to Features and Epics.** The `--feature` and `--epic` flags let you generate specs for every child work item in one command, with a summary index file linking them all together.

---

## Mock mode

```bash
dotnet backlog-2-spec spec 12345 --mock
```

Mock mode replaces every external dependency — ADO client, enrichment agent, spec generator — with fast stub implementations that return fixed data. No credentials are required and no network calls are made.

Use it to:
- Verify your `backlog-2-spec.json` config is found and parsed correctly
- Test output formatting and rendering without waiting for real AI responses
- Try the full pipeline in CI or on a machine without secrets configured

Mock mode is detected at startup (before the DI container is built), so it works even if `AzureAI:*` secrets are not set.

---

## Keeping the tool up to date

When you pull changes from this repo, rebuild and reinstall:

```bash
cd path/to/Backlog2SpecAgent
git pull
dotnet pack src/Backlog2SpecAgent.Cli -o ./nupkg
dotnet tool update --local --add-source ./nupkg Backlog2SpecAgent.Cli
```

Use `--global` instead of `--local` if you installed globally.

---

## Troubleshooting

| Error | Cause | Fix |
|---|---|---|
| `Configuration error: 'backlog-2-spec.json' not found` | No config file in CWD or any parent | Create `backlog-2-spec.json` in your project root |
| `Missing required field: ado.organization` | Config file incomplete | Add the missing field |
| `Configuration error: devRulesFile not found: '...'` | Path in `devRulesFile` does not exist | Check the path is relative to `backlog-2-spec.json` and the file exists |
| `Authentication error: Failed to connect to Azure DevOps` | Invalid PAT or org URL | Re-set `Ado:Pat` and verify `ado.organization` |
| `Authentication error: Authentication failed` | PAT expired or wrong scope | Generate a new PAT with Work Items: Read (and Code: Read if using repo context) |
| `AI response error: LLM returned invalid JSON` | Model returned malformed JSON after 3 retries | Check deployment name and quota; try again |
| `Unexpected error: AzureAI:Endpoint secret is missing` | User secrets not set | Run the secrets setup commands in Step 5 |
| `No manifest file found` | Missing `.config/dotnet-tools.json` in project | Run `dotnet new tool-manifest` in your project root, then reinstall |
