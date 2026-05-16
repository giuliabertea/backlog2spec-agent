## 1. AgentConfig Model

- [x] 1.1 Add `DevRulesFile` nullable string property (with `init`) to `AgentConfig` for JSON deserialization
- [x] 1.2 Add `DevRulesContent` nullable string property (with `internal set`) to `AgentConfig` for the loaded content

## 2. ConfigLoader

- [x] 2.1 After `ValidateRequiredFields`, check if `config.DevRulesFile` is non-null and non-empty
- [x] 2.2 Resolve the path relative to the config file directory (not CWD)
- [x] 2.3 If the resolved file does not exist, throw `ConfigException` with the resolved path in the message
- [x] 2.4 Read the file content and assign it to `config.DevRulesContent`

## 3. Prompt Templates

- [x] 3.1 Add `{{devRules}}` placeholder to `Prompts/enrichment.txt` at the appropriate location (after codebase context, before the task instructions)
- [x] 3.2 Add `{{devRules}}` placeholder to `Prompts/spec.txt` at the appropriate location (after codebase context, before the task instructions)

## 4. EnrichmentAgent

- [x] 4.1 In `BuildPrompt`, compute the dev rules block: `"## Development Rules\n\n" + content` when `DevRulesContent` is non-null/non-empty, otherwise empty string
- [x] 4.2 Add `.Replace("{{devRules}}", devRulesBlock)` to the template substitution chain

## 5. SpecGeneratorAgent

- [x] 5.1 In `BuildPrompt`, compute the dev rules block: `"## Development Rules\n\n" + content` when `DevRulesContent` is non-null/non-empty, otherwise empty string
- [x] 5.2 Add `.Replace("{{devRules}}", devRulesBlock)` to the template substitution chain

## 6. Verification

- [x] 6.1 Add a `backlog-2-spec.json` with `"devRulesFile"` pointing to an existing file and run the tool — confirm dev rules appear in enrichment output
- [x] 6.2 Run without `devRulesFile` — confirm existing behavior is unchanged
- [x] 6.3 Set `devRulesFile` to a non-existent path — confirm `ConfigException` is thrown with the resolved path
