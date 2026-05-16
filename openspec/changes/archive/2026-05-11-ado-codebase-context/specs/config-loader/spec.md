## ADDED Requirements

### Requirement: Support optional ADO repository configuration
`AgentConfig.AdoConfig` SHALL expose two new optional fields: `RepoName` (string?, default null) and `Branch` (string?, default null). Neither field is required. When `RepoName` is set and `Branch` is absent or null, the `CodebaseContextAgent` SHALL treat the branch as `"master"`. `ConfigLoader` SHALL NOT throw if these fields are absent from `backlog-2-spec.json`.

#### Scenario: Config without repoName or branch loads successfully
- **WHEN** `backlog-2-spec.json` does not contain `ado.repoName` or `ado.branch`
- **THEN** `ConfigLoader.LoadAsync()` succeeds and `AgentConfig.Ado.RepoName` is null

#### Scenario: Config with repoName and branch populates both fields
- **WHEN** `backlog-2-spec.json` contains `"repoName": "CCC"` and `"branch": "develop"`
- **THEN** `AgentConfig.Ado.RepoName` is `"CCC"` and `AgentConfig.Ado.Branch` is `"develop"`

#### Scenario: Config with repoName but no branch defaults to master
- **WHEN** `backlog-2-spec.json` contains `"repoName": "CCC"` but no `"branch"` key
- **THEN** `AgentConfig.Ado.Branch` is null and `CodebaseContextAgent` uses `"master"` as the branch when building API URLs

## REMOVED Requirements

### Requirement: WikiName field in AdoConfig
**Reason:** Replaced by `RepoName` and `Branch` as part of the shift from wiki to Git repository context.
**Migration:** Remove `ado.wikiName` from `backlog-2-spec.json` and add `ado.repoName` (and optionally `ado.branch`). The `WikiContextAgent` and `WikiPageDto` types are deleted; no runtime fallback exists.
