<img width="142" height="150" alt="backlog2spec_current_architecture" src="https://github.com/user-attachments/assets/8c2621b3-d9fd-463c-9d3a-d459a25e5bba" />
# Backlog2SpecAgent

A CLI tool that turns an Azure DevOps work item into a structured, ready-to-use spec — in seconds.

Given a work item ID (PBI, tus, bug, feature, epic), it fetches the ticket from ADO, enriches it with AI (filling context, edge cases, ambiguities), optionally retrieves relevant source files from your indexed codebase for grounding, then generates a structured spec tailored to your project's stack and conventions.

---

## Commands

```bash
# Generate a spec for a single work item (PBI, TUS, BUG)
backlog-2-spec-agent spec 12345

# Generate a spec for a single work item and save to markdown file
backlog-2-spec-agent spec 12345 --output ./spec/feature-12345.md

#  Generate a spec for a single work item in JSON format (pipe-friendly)
backlog-2-spec-agent spec 12345 --raw

# Export all children of a Feature or Epic
backlog-2-spec-agent spec 12345 --feature
backlog-2-spec-agent spec 12345 --epic
```

`--feature` and `--epic` generate a spec per child work item and write them to `spec/<id>-<slug>/`, with a `_summary.md` index. They are mutually exclusive.

---

## Spec output format

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

## Azure environment setup

The ARM template in `infra/azuredeploy.json` provisions everything you need:
- Azure AI Services account with a GPT-4o deployment
- Azure AI Foundry hub and project
- Azure AI Search service (for RAG)

### 1. Deploy with Azure CLI

```bash
az group create --name b2s-rg --location eastus

az deployment group create \
  --resource-group b2s-rg \
  --template-file infra/azuredeploy.json \
  --parameters prefix=b2s location=eastus

# Read the outputs — you will need these values when setting secrets
az deployment group show \
  --resource-group b2s-rg \
  --name azuredeploy \
  --query properties.outputs
```

### 2. Deploy with PowerShell

```powershell
New-AzResourceGroup -Name b2s-rg -Location eastus

New-AzResourceGroupDeployment `
  -ResourceGroupName b2s-rg `
  -TemplateFile infra/azuredeploy.json `
  -prefix b2s `
  -location eastus

(Get-AzResourceGroupDeployment -ResourceGroupName b2s-rg -Name azuredeploy).Outputs
```

### 3. Deploy from the Azure Portal

Go to **portal.azure.com → Deploy a custom template → Build your own template in the editor**, paste `infra/azuredeploy.json`, fill in `prefix` and `location`, then deploy.

> **GPT-4o regions:** `eastus`, `eastus2`, `swedencentral`, `australiaeast`, `westus`, `westus3`. Use the same region for all resources.

> **Search SKU:** `free` works for small repos (≤ 50 MB, 1 per subscription). Use `basic` for a real codebase.

### Create the Azure OpenAI Assistant

1. Go to [oai.azure.com](https://oai.azure.com) → **Assistants → Create**.
2. Name it (e.g. `backlog2spec-agent`) and note the **Assistant ID** — you will need it as `AzureAI:AssistantId`.
3. Select the `gpt-4o` deployment.
4. Paste this system prompt:

```
You are a senior software engineer generating production-ready structured specs
from Azure DevOps work items.

You receive a JSON object with:
  workItem:      the ADO ticket data (id, title, description, acceptanceCriteria)
  projectConfig: stack information
  repoContext:   relevant source file snippets

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
- Reference real file paths from repoContext when available
- Be complete: cover all edge cases implied by the ticket
```

5. Save. No tool definitions are needed in the portal.

### Index your codebase for RAG (run once, repeat after major changes)

```powershell
.\scripts\index-repo.ps1 `
    -SearchUrl  "https://<your-search>.search.windows.net" `
    -SearchKey  "<admin-key>" `
    -RepoPath   "C:\path\to\your-project"
```

Add the Azure AI Search secrets:

```bash
dotnet user-secrets set "AzureSearch:Endpoint"   "https://<name>.search.windows.net"
dotnet user-secrets set "AzureSearch:ApiKey"      "your-search-admin-key"
dotnet user-secrets set "AzureSearch:IndexName"   "codebase-chunks"
```

The script scans `.cs` and `.md` files, splits them into 300–500 line chunks, and upserts them to the index. It is safe to re-run.

### Create an Azure DevOps PAT

Each developer needs their own Personal Access Token.

1. Go to `https://dev.azure.com/{your-org}/_usersSettings/tokens` → **New Token**.
2. Set **Work Items: Read** (add **Code: Read** if you want source-file context from your ADO repo).
3. Copy the token — you will not see it again.

---

## CLI installation

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

> Use `--local` instead of `--global` if you prefer a per-project install. Run `dotnet new tool-manifest` in your project directory first.

### Set secrets

Run from inside the `Backlog2SpecAgent/src/Backlog2SpecAgent.Cli` directory. These are per-machine and never stored in files.

```bash
dotnet user-secrets set "AzureAI:Endpoint"       "https://<name>.openai.azure.com"
dotnet user-secrets set "AzureAI:ApiKey"          "your-api-key"
dotnet user-secrets set "AzureAI:DeploymentName"  "gpt-4o"
dotnet user-secrets set "AzureAI:AssistantId"     "asst_..."

dotnet user-secrets set "Ado:Pat"                 "your-ado-pat"
```

> If you used the ARM template, copy `aiServicesEndpoint`, `aiServicesKey`, and `gptDeploymentName` directly from the deployment outputs.

### Add the project config file (optional but recommended)

Place `backlog-2-spec.json` in the root of **your project** (not the Backlog2SpecAgent repo). The tool searches upward from the current directory to find it. Without it, the tool falls back to built-in defaults.

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

`ado.organization` and `ado.project` are required when the file is present. `repoName` and `branch` enable source-file context fetching from your ADO repo. `devRulesFile` injects team-specific rules into every prompt (see [Project rules file](#project-rules-file)).

Commit this file — it contains no secrets.

---

## Running the tool

### Smoke test

```bash
cd path/to/your-project
backlog-2-spec-agent spec 1 --mock
```

If it prints a spec, the tool is installed correctly.

### Live run

```bash
cd path/to/your-project
backlog-2-spec-agent spec 12345
```
### Update the tool

```bash
cd path/to/Backlog2SpecAgent
git pull
dotnet pack src/Backlog2SpecAgent.Cli -o ./nupkg
dotnet tool update --global --add-source ./nupkg Backlog2SpecAgent.Cli
```

---

## Other useful details

### Using specs with AI coding assistants

Save a spec with `--output`, attach it in Copilot Chat or Cursor, and use:

```
Implement the spec in the attached file.
Follow the "Files to Change" section exactly — only touch the listed files.
Do not implement anything listed under "Out of Scope".
```

For a full feature export (`--feature` or `--epic`):

```
Implement the following feature step by step.

The feature spec is in `spec/<folder>/00-feature.md`.
Each PBI spec is a numbered file in the same folder.

Rules:
1. Read the feature spec first to understand the overall goal and scope.
2. Implement each PBI in file order (01, 02, …), one at a time.
3. For each PBI, follow the "Files to Change" section exactly.
4. Respect "Out of Scope" — do not implement anything listed there.
5. After each PBI, summarise what you changed before moving to the next.
6. Do not refactor code outside of what the spec asks for.

Start with the feature spec, confirm your understanding, then begin PBI 01.
```

### Project rules file

`devRulesFile` points to a markdown file that gets injected verbatim into every prompt. Use it for constraints that are too nuanced for the structured config fields.

```markdown
# dev-rules.md

- Never put business logic in controllers.
- All service methods return Result<T>. Never throw exceptions from the service layer.
- Do not use AutoMapper. All mapping is done in dedicated mapper classes.
- The domain layer must not reference any infrastructure or application layer types.
- Repository interfaces live in the domain layer; implementations in the infrastructure layer.
- Commands and queries follow MediatR: VerbNounCommand / VerbNounCommandHandler.
```

Reference it in `backlog-2-spec.json` with `"devRulesFile": "dev-rules.md"` and commit both files.

### Overall architecture
<svg width="100%" viewBox="0 0 680 720" role="img" style="" xmlns="http://www.w3.org/2000/svg">
  <title style="fill:rgb(0, 0, 0);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto">Backlog2SpecAgent current architecture</title>
  <desc style="fill:rgb(0, 0, 0);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto">Data flow from CLI through Tools API (App Service) to Classic OpenAI Assistant, with Azure AI Search providing RAG context</desc>
  <defs>
    <marker id="arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
      <path d="M2 1L8 5L2 9" fill="none" stroke="context-stroke" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
    </marker>
  <mask id="imagine-text-gaps-vbf2qn" maskUnits="userSpaceOnUse"><rect x="0" y="0" width="680" height="720" fill="white"/><rect x="3.60546875" y="80.5" width="64.9539794921875" height="19" fill="black" rx="2"/><rect x="16.2578125" y="240.5" width="39.53398132324219" height="19" fill="black" rx="2"/><rect x="10.1953125" y="510.5" width="51.609375" height="19" fill="black" rx="2"/><rect x="242.29296875" y="71.25" width="195.4140625" height="21.5" fill="black" rx="2"/><rect x="242.03515625" y="94.5" width="195.9296875" height="19" fill="black" rx="2"/><rect x="50.48046875" y="80.5" width="90.5390625" height="19" fill="black" rx="2"/><rect x="80.5" y="172.5" width="139.84375" height="19" fill="black" rx="2"/><rect x="132.96484375" y="203.25" width="136.0703125" height="21.5" fill="black" rx="2"/><rect x="126.609375" y="222.5" width="148.78125" height="19" fill="black" rx="2"/><rect x="131.23828125" y="261.25" width="139.5379638671875" height="21.5" fill="black" rx="2"/><rect x="98.19140625" y="280.5" width="205.6171875" height="19" fill="black" rx="2"/><rect x="450.30859375" y="215.25" width="114.8828125" height="21.5" fill="black" rx="2"/><rect x="410.6953125" y="238.5" width="194.109375" height="19" fill="black" rx="2"/><rect x="402.4140625" y="282.5" width="211.223876953125" height="19" fill="black" rx="2"/><rect x="431.51171875" y="298.5" width="153.12591552734375" height="19" fill="black" rx="2"/><rect x="221.9921875" y="425.25" width="236.015625" height="21.5" fill="black" rx="2"/><rect x="224.953125" y="448.5" width="230.09375" height="19" fill="black" rx="2"/><rect x="197.03515625" y="464.5" width="285.9296875" height="19" fill="black" rx="2"/><rect x="88.2421875" y="326.5" width="87.515625" height="19" fill="black" rx="2"/><rect x="215.79296875" y="384" width="248.4140625" height="19" fill="black" rx="2"/><rect x="512.75390625" y="326.5" width="70.4921875" height="19" fill="black" rx="2"/><rect x="12.66796875" y="548.5" width="46.6640625" height="19" fill="black" rx="2"/><rect x="254.515625" y="557.25" width="171.346923828125" height="21.5" fill="black" rx="2"/><rect x="212.20703125" y="578.5" width="255.13385009765625" height="19" fill="black" rx="2"/><rect x="80" y="634.5" width="155.8515625" height="19" fill="black" rx="2"/><rect x="95.234375" y="660.5" width="117.79794311523438" height="19" fill="black" rx="2"/><rect x="232.1015625" y="660.5" width="175.296875" height="19" fill="black" rx="2"/><rect x="410.8515625" y="660.5" width="205.796875" height="19" fill="black" rx="2"/></mask></defs>

  <!-- === LAYER LABELS === -->
  <text x="36" y="90" text-anchor="middle" dominant-baseline="central" transform="rotate(-90,36,90)" opacity="0.5" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.5;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">developer</text>
  <text x="36" y="250" text-anchor="middle" dominant-baseline="central" transform="rotate(-90,36,250)" opacity="0.5" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.5;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">azure</text>
  <text x="36" y="520" text-anchor="middle" dominant-baseline="central" transform="rotate(-90,36,520)" opacity="0.5" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.5;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">azure ai</text>

  <!-- Horizontal dividers -->
  <line x1="60" y1="148" x2="648" y2="148" stroke="var(--b)" stroke-width="0.5" stroke-dasharray="4 4" opacity="0.4" style="fill:rgb(0, 0, 0);stroke:rgba(31, 30, 29, 0.3);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-dasharray:4px, 4px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.4;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
  <line x1="60" y1="380" x2="648" y2="380" stroke="var(--b)" stroke-width="0.5" stroke-dasharray="4 4" opacity="0.4" style="fill:rgb(0, 0, 0);stroke:rgba(31, 30, 29, 0.3);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-dasharray:4px, 4px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.4;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>

  <!-- ===== ROW 1: CLI ===== -->
  <!-- CLI box -->
  <g onclick="sendPrompt('Tell me more about what the CLI does in Backlog2SpecAgent')" style="fill:rgb(0, 0, 0);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto">
    <rect x="240" y="54" width="200" height="72" rx="8" stroke-width="0.5" style="fill:rgb(238, 237, 254);stroke:rgb(83, 74, 183);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
    <text x="340" y="82" text-anchor="middle" dominant-baseline="central" style="fill:rgb(60, 52, 137);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:14px;font-weight:500;text-anchor:middle;dominant-baseline:central">CLI (dotnet backlog-2-spec)</text>
    <text x="340" y="104" text-anchor="middle" dominant-baseline="central" style="fill:rgb(83, 74, 183);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">fetches work item, builds payload</text>
  </g>

  <!-- ADO label on left -->
  <text x="96" y="90" text-anchor="middle" dominant-baseline="central" opacity="0.7" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.7;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">Azure DevOps</text>
  <line x1="148" y1="90" x2="238" y2="90" marker-end="url(#arrow)" stroke="#7F77DD" style="fill:none;stroke:rgb(115, 114, 108);color:rgb(0, 0, 0);stroke-width:1.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>

  <!-- ===== ROW 2: App Service + AI Search ===== -->

  <!-- App Service container (dashed) -->
  <rect x="68" y="166" width="280" height="196" rx="10" fill="none" stroke="var(--b)" stroke-width="0.5" stroke-dasharray="5 4" style="fill:none;stroke:rgba(31, 30, 29, 0.3);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-dasharray:5px, 4px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
  <text x="85" y="182" dominant-baseline="central" opacity="0.6" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.6;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:start;dominant-baseline:central">App Service (Tools API)</text>

  <!-- GetWorkItem endpoint -->
  <g onclick="sendPrompt('What does the GetWorkItem endpoint do in the Tools API?')" style="fill:rgb(0, 0, 0);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto">
    <rect x="86" y="194" width="230" height="48" rx="6" stroke-width="0.5" style="fill:rgb(225, 245, 238);stroke:rgb(15, 110, 86);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
    <text x="201" y="214" text-anchor="middle" dominant-baseline="central" style="fill:rgb(8, 80, 65);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:14px;font-weight:500;text-anchor:middle;dominant-baseline:central">GET /workitem/{id}</text>
    <text x="201" y="232" text-anchor="middle" dominant-baseline="central" style="fill:rgb(15, 110, 86);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">proxies to Azure DevOps</text>
  </g>

  <!-- RepoContext endpoint -->
  <g onclick="sendPrompt('What does the RepoContext endpoint do in the Tools API?')" style="fill:rgb(0, 0, 0);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto">
    <rect x="86" y="252" width="230" height="48" rx="6" stroke-width="0.5" style="fill:rgb(225, 245, 238);stroke:rgb(15, 110, 86);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
    <text x="201" y="272" text-anchor="middle" dominant-baseline="central" style="fill:rgb(8, 80, 65);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:14px;font-weight:500;text-anchor:middle;dominant-baseline:central">POST /repo-context</text>
    <text x="201" y="290" text-anchor="middle" dominant-baseline="central" style="fill:rgb(15, 110, 86);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">queries AI Search, returns snippets</text>
  </g>

  <!-- AI Search box -->
  <g onclick="sendPrompt('How does Azure AI Search work in Backlog2SpecAgent?')" style="fill:rgb(0, 0, 0);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto">
    <rect x="382" y="194" width="252" height="152" rx="8" stroke-width="0.5" style="fill:rgb(230, 241, 251);stroke:rgb(24, 95, 165);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
    <text x="508" y="226" text-anchor="middle" dominant-baseline="central" style="fill:rgb(12, 68, 124);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:14px;font-weight:500;text-anchor:middle;dominant-baseline:central">Azure AI Search</text>
    <text x="508" y="248" text-anchor="middle" dominant-baseline="central" style="fill:rgb(24, 95, 165);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">vector index of codebase chunks</text>
    <text x="508" y="292" text-anchor="middle" dominant-baseline="central" style="fill:rgb(24, 95, 165);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">embedding: text-embedding-3-large</text>
    <text x="508" y="308" text-anchor="middle" dominant-baseline="central" style="fill:rgb(24, 95, 165);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">semantic search on query</text>
  </g>

  <!-- Arrow: CLI → GetWorkItem -->
  <path d="M 340 126 L 340 160 L 201 160 L 201 192" fill="none" marker-end="url(#arrow)" stroke="#7F77DD" mask="url(#imagine-text-gaps-vbf2qn)" style="fill:none;stroke:rgb(115, 114, 108);color:rgb(0, 0, 0);stroke-width:1.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>

  <!-- Arrow: GetWorkItem → CLI (return) not drawn, implied -->

  <!-- Arrow: CLI → RepoContext -->
  <!-- implicit, same call path -->

  <!-- Arrow: RepoContext → AI Search -->
  <line x1="316" y1="276" x2="380" y2="276" marker-end="url(#arrow)" stroke="#378ADD" style="fill:none;stroke:rgb(115, 114, 108);color:rgb(0, 0, 0);stroke-width:1.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>

  <!-- ===== ROW 3: Classic OpenAI Assistant ===== -->

  <!-- Assistant box -->
  <g onclick="sendPrompt('How does the Classic OpenAI Assistant work in Backlog2SpecAgent?')" style="fill:rgb(0, 0, 0);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto">
    <rect x="140" y="408" width="400" height="80" rx="8" stroke-width="0.5" style="fill:rgb(250, 236, 231);stroke:rgb(153, 60, 29);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
    <text x="340" y="436" text-anchor="middle" dominant-baseline="central" style="fill:rgb(113, 43, 19);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:14px;font-weight:500;text-anchor:middle;dominant-baseline:central">Classic OpenAI Assistant (asst_...)</text>
    <text x="340" y="458" text-anchor="middle" dominant-baseline="central" style="fill:rgb(153, 60, 29);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">GPT-4o · api-key auth · Assistants API v1</text>
    <text x="340" y="474" text-anchor="middle" dominant-baseline="central" style="fill:rgb(153, 60, 29);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">receives single JSON payload, returns spec JSON</text>
  </g>

  <!-- Arrow: CLI sends payload to Assistant -->
  <path d="M 340 310 L 340 340 L 220 340 L 220 406" fill="none" marker-end="url(#arrow)" stroke="#D85A30" mask="url(#imagine-text-gaps-vbf2qn)" style="fill:none;stroke:rgb(115, 114, 108);color:rgb(0, 0, 0);stroke-width:1.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
  <text x="132" y="336" text-anchor="middle" dominant-baseline="central" opacity="0.7" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.7;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">JSON payload</text>

  <!-- Arrow: RepoContext result also feeds into payload (via CLI) - just show dotted label -->
  <text x="340" y="398" text-anchor="middle" opacity="0.55" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.55;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:auto">(workItem + projectConfig + repoContext)</text>

  <!-- Arrow: Assistant result back to CLI -->
  <path d="M 460 406 L 460 340 L 480 340 L 480 90 L 442 90" fill="none" marker-end="url(#arrow)" stroke="#D85A30" mask="url(#imagine-text-gaps-vbf2qn)" style="fill:none;stroke:rgb(115, 114, 108);color:rgb(0, 0, 0);stroke-width:1.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
  <text x="548" y="336" text-anchor="middle" dominant-baseline="central" opacity="0.7" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.7;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">spec JSON</text>

  <!-- ===== OUTPUT ===== -->
  <line x1="60" y1="528" x2="648" y2="528" stroke="var(--b)" stroke-width="0.5" stroke-dasharray="4 4" opacity="0.4" mask="url(#imagine-text-gaps-vbf2qn)" style="fill:rgb(0, 0, 0);stroke:rgba(31, 30, 29, 0.3);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-dasharray:4px, 4px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.4;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
  <text x="36" y="558" text-anchor="middle" dominant-baseline="central" transform="rotate(-90,36,590)" opacity="0.5" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.5;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">output</text>

  <g onclick="sendPrompt('What does the spec output file look like?')" style="fill:rgb(0, 0, 0);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto">
    <rect x="190" y="548" width="300" height="56" rx="8" stroke-width="0.5" style="fill:rgb(241, 239, 232);stroke:rgb(95, 94, 90);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
    <text x="340" y="568" text-anchor="middle" dominant-baseline="central" style="fill:rgb(68, 68, 65);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:14px;font-weight:500;text-anchor:middle;dominant-baseline:central">spec.json written to disk</text>
    <text x="340" y="588" text-anchor="middle" dominant-baseline="central" style="fill:rgb(95, 94, 90);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:1;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">goal · behaviour · edgeCases · filesToChange</text>
  </g>

  <path d="M 340 490 L 340 546" fill="none" marker-end="url(#arrow)" stroke="var(--t)" opacity="0.5" style="fill:none;stroke:rgb(115, 114, 108);color:rgb(0, 0, 0);stroke-width:1.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.5;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>

  <!-- ===== LEGEND: what is NOT needed ===== -->
  <rect x="68" y="626" width="560" height="72" rx="8" fill="none" stroke="var(--b)" stroke-width="0.5" stroke-dasharray="3 3" opacity="0.5" style="fill:none;stroke:rgba(31, 30, 29, 0.3);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-dasharray:3px, 3px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.5;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
  <text x="84" y="644" dominant-baseline="central" opacity="0.6" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.6;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:start;dominant-baseline:central">not used / can be removed</text>
  <rect x="84" y="656" width="140" height="28" rx="6" fill="none" stroke="var(--b)" stroke-width="0.5" opacity="0.4" style="fill:none;stroke:rgba(31, 30, 29, 0.3);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.4;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
  <text x="154" y="670" text-anchor="middle" dominant-baseline="central" opacity="0.5" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.5;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">Foundry Agent SDK</text>
  <rect x="240" y="656" width="160" height="28" rx="6" fill="none" stroke="var(--b)" stroke-width="0.5" opacity="0.4" style="fill:none;stroke:rgba(31, 30, 29, 0.3);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.4;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
  <text x="320" y="670" text-anchor="middle" dominant-baseline="central" opacity="0.5" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.5;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">AzureCliCredential / TenantId</text>
  <rect x="416" y="656" width="196" height="28" rx="6" fill="none" stroke="var(--b)" stroke-width="0.5" opacity="0.4" style="fill:none;stroke:rgba(31, 30, 29, 0.3);color:rgb(0, 0, 0);stroke-width:0.5px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.4;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:16px;font-weight:400;text-anchor:start;dominant-baseline:auto"/>
  <text x="514" y="670" text-anchor="middle" dominant-baseline="central" opacity="0.5" style="fill:rgb(61, 61, 58);stroke:none;color:rgb(0, 0, 0);stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;opacity:0.5;font-family:&quot;Anthropic Sans&quot;, -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, sans-serif;font-size:12px;font-weight:400;text-anchor:middle;dominant-baseline:central">Tool callbacks (get_work_item etc.)</text>
</svg>

---<img width="142" height="150" alt="backlog2spec_current_architecture" src="https://github.com/user-attachments/assets/3146e7ef-ac9e-4d6c-9cb2-6945e1257610" />


### Troubleshooting

| Error | Cause | Fix |
|---|---|---|
| `Missing required field: ado.organization` | Config file incomplete | Add the missing field |
| `Configuration error: devRulesFile not found` | Path does not exist | Check the path is relative to `backlog-2-spec.json` |
| `Authentication error: Failed to connect to Azure DevOps` | Invalid PAT or org URL | Re-set `Ado:Pat` and verify `ado.organization` |
| `Authentication error: PAT expired or wrong scope` | PAT expired | Generate a new PAT with Work Items: Read |
| `AI response error: LLM returned invalid JSON` | Model returned malformed JSON | Check deployment name and quota; try again |
| `Unexpected error: AzureAI:Endpoint secret is missing` | User secrets not set | Run the secrets setup commands in the installation guide |
| `Unexpected error: AzureAI:AssistantId secret is missing` | Assistant ID not set | Copy the ID from Azure OpenAI Studio and set the secret |
| `No manifest file found` | Missing `.config/dotnet-tools.json` | Run `dotnet new tool-manifest` in your project root |
| `POST /threads → 401` | Wrong API key | Verify `AzureAI:ApiKey` matches the key under your Azure OpenAI resource |
| `POST /threads → 404` | Wrong endpoint format | Ensure `AzureAI:Endpoint` points to your Azure OpenAI resource URL |
| `Run <id> ended with status 'failed'` | Assistant run failed | Check the assistant config in Azure OpenAI Studio; verify deployment name is `gpt-4o` |
| `Azure AI Search → 401` | Wrong search API key | Use the **admin key** from Azure AI Search → Settings → Keys |
| `Azure AI Search → 404` | Wrong index name or URL | Check `AzureSearch:IndexName` matches the index created by `index-repo.ps1` |
| `index-repo.ps1` fails with 401 | Wrong search key | Use the admin key from Azure AI Search → Settings → Keys |
| `GPT-4o deployment fails with ModelNotFound` | Wrong region | Redeploy to `eastus`, `swedencentral`, or `australiaeast` |
