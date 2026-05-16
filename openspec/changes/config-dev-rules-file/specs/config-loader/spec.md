## ADDED Requirements

### Requirement: Load development rules from optional devRulesFile path
When `backlog-2-spec.json` contains a `devRulesFile` field, `ConfigLoader` SHALL resolve the path relative to the directory of `backlog-2-spec.json`, read the file content as a UTF-8 string, and assign it to `AgentConfig.DevRulesContent`. If `devRulesFile` is absent or empty, `AgentConfig.DevRulesContent` SHALL remain null. If the field is set but the file does not exist at the resolved path, `ConfigLoader` SHALL throw a `ConfigException` with a message that includes the resolved path.

#### Scenario: devRulesFile absent leaves DevRulesContent null
- **WHEN** `backlog-2-spec.json` does not contain a `devRulesFile` field
- **THEN** `ConfigLoader.LoadAsync()` succeeds and `AgentConfig.DevRulesContent` is null

#### Scenario: devRulesFile set to existing file populates DevRulesContent
- **WHEN** `backlog-2-spec.json` contains `"devRulesFile": "dev-rules.md"` and `dev-rules.md` exists alongside the config file
- **THEN** `AgentConfig.DevRulesContent` contains the full UTF-8 text content of that file

#### Scenario: devRulesFile set to missing file throws ConfigException
- **WHEN** `backlog-2-spec.json` contains `"devRulesFile": "missing.md"` but no such file exists at the resolved path
- **THEN** `ConfigLoader.LoadAsync()` throws `ConfigException` with a message containing the resolved file path

#### Scenario: devRulesFile path is resolved relative to config file directory
- **WHEN** `backlog-2-spec.json` is located at `/project/backlog-2-spec.json` and `devRulesFile` is `"docs/rules.md"`
- **THEN** `ConfigLoader` reads from `/project/docs/rules.md`, not from the current working directory
