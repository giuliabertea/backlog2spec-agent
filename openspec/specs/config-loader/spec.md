# config-loader

## Purpose

`ConfigLoader` is responsible for locating, loading, and validating the `backlog-2-spec.json` configuration file. It performs upward directory traversal from the current working directory, deserializes the file using `System.Text.Json`, and validates required fields before returning a populated `AgentConfig`.

## Requirements

### Requirement: Locate backlog-2-spec.json by upward directory search
`ConfigLoader` SHALL search for `backlog-2-spec.json` starting from the current working directory and walking upward toward the filesystem root. The first match found SHALL be loaded. If no file is found, `ConfigLoader` SHALL throw a `ConfigException` with a clear message indicating the search path.

#### Scenario: Config found in current directory
- **WHEN** `backlog-2-spec.json` exists in the current working directory
- **THEN** `ConfigLoader.LoadAsync()` returns a populated `AgentConfig` without error

#### Scenario: Config found in parent directory
- **WHEN** `backlog-2-spec.json` does not exist in the CWD but exists in a parent directory
- **THEN** `ConfigLoader.LoadAsync()` finds and loads the parent's config

#### Scenario: Config not found anywhere throws ConfigException
- **WHEN** no `backlog-2-spec.json` exists in the CWD or any ancestor directory
- **THEN** `ConfigLoader.LoadAsync()` throws `ConfigException` with a message that includes the searched path

### Requirement: Validate required fields and fail fast
After loading `backlog-2-spec.json`, `ConfigLoader` SHALL validate that all required fields are present and non-empty: `ado.organization`, `ado.project`, `project.name`. If any required field is missing or empty, `ConfigLoader` SHALL throw a `ConfigException` naming the missing field.

#### Scenario: Missing ado.organization throws ConfigException
- **WHEN** `backlog-2-spec.json` is loaded but `ado.organization` is absent or empty
- **THEN** `ConfigLoader.LoadAsync()` throws `ConfigException` with "Missing required field: ado.organization"

#### Scenario: Missing ado.project throws ConfigException
- **WHEN** `backlog-2-spec.json` is loaded but `ado.project` is absent or empty
- **THEN** `ConfigLoader.LoadAsync()` throws `ConfigException` with "Missing required field: ado.project"

#### Scenario: All required fields present returns AgentConfig
- **WHEN** `backlog-2-spec.json` contains all required fields
- **THEN** `ConfigLoader.LoadAsync()` returns a fully populated `AgentConfig` with no exception

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

### Requirement: Deserialize config using System.Text.Json with camelCase
`ConfigLoader` SHALL deserialize `backlog-2-spec.json` using `System.Text.Json` with `JsonNamingPolicy.CamelCase` and `PropertyNameCaseInsensitive = true`. No `Newtonsoft.Json` dependency SHALL be introduced.

#### Scenario: camelCase JSON keys map to PascalCase C# properties
- **WHEN** `backlog-2-spec.json` contains `"organization"` (camelCase)
- **THEN** `AgentConfig.Ado.Organization` is populated correctly
