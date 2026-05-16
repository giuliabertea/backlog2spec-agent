## 1. Add endpoint type enum and update KernelFactory

- [x] 1.1 Add `AiEndpointType` enum (`AzureOpenAI`, `AzureFoundry`) in `src/Backlog2SpecAgent.Cli/Kernel/`
- [x] 1.2 Update `KernelFactory.Build` to accept `AiEndpointType endpointType = AiEndpointType.AzureOpenAI`
- [x] 1.3 Branch inside `Build`: use `AddAzureOpenAIChatCompletion` for `AzureOpenAI`, `AddOpenAIChatCompletion` with custom URI for `AzureFoundry`
- [x] 1.4 Throw `InvalidOperationException` for any unrecognised endpoint type value (with message listing valid options)

## 2. Update Program.cs secret key reads

- [x] 2.1 Replace all `config["AzureOpenAI:*"]` reads with `config["AzureAI:*"]` equivalents
- [x] 2.2 Read optional `config["AzureAI:EndpointType"]`; parse into `AiEndpointType` (default `AzureOpenAI`); throw on unknown value
- [x] 2.3 Pass `endpointType` to `KernelFactory.Build`

## 3. Update README

- [x] 3.1 Update the **Prerequisites** line to reference Azure AI Foundry as the primary option and Azure OpenAI as the alternative
- [x] 3.2 Replace the `dotnet user-secrets set` commands block: rename keys to `AzureAI:*` and add the optional `AzureAI:EndpointType` line with a comment
- [x] 3.3 Add a short **Migration from Azure OpenAI secrets** note (old key → new key mapping) above the secrets block
- [x] 3.4 Update the troubleshooting table: replace `AzureOpenAI:Endpoint secret is missing` row with `AzureAI:Endpoint secret is missing`

## 4. Verify end-to-end

- [x] 4.1 Run `dotnet build` and confirm no compile errors
- [x] 4.2 Run `dotnet backlog-2-spec spec <id> --mock` to confirm mock mode still works
- [x] 4.3 Run the test suite (`dotnet test`) and confirm all tests pass
