# Spec: azure-ai-connector

## Purpose

Registers the appropriate Semantic Kernel chat completion service based on Azure AI configuration, supporting both Azure OpenAI and Azure AI Foundry serverless endpoint types. Validates configuration at startup and fails fast on missing or unrecognised values.

## Requirements

### Requirement: Connector supports Azure OpenAI endpoint type
The system SHALL register a Semantic Kernel chat completion service using the Azure OpenAI connector (`AddAzureOpenAIChatCompletion`) when `AzureAI:EndpointType` is set to `AzureOpenAI` or is absent. The endpoint URL MUST follow the Azure OpenAI resource format (`https://<name>.openai.azure.com`).

#### Scenario: Default endpoint type is Azure OpenAI
- **WHEN** `AzureAI:EndpointType` is not set in user secrets
- **THEN** the kernel is built using `AddAzureOpenAIChatCompletion` with the provided endpoint and API key

#### Scenario: Explicit Azure OpenAI endpoint type
- **WHEN** `AzureAI:EndpointType` is set to `AzureOpenAI`
- **THEN** the kernel is built using `AddAzureOpenAIChatCompletion` with the provided endpoint and API key

### Requirement: Connector supports Azure AI Foundry serverless endpoint type
The system SHALL register a Semantic Kernel chat completion service using the OpenAI-compatible connector (`AddOpenAIChatCompletion` with a custom URI) when `AzureAI:EndpointType` is set to `AzureFoundry`. The endpoint URL is treated as an OpenAI-compatible base URL.

#### Scenario: Azure AI Foundry endpoint type
- **WHEN** `AzureAI:EndpointType` is set to `AzureFoundry`
- **THEN** the kernel is built using `AddOpenAIChatCompletion` with `modelId = AzureAI:DeploymentName`, `apiKey = AzureAI:ApiKey`, and `endpoint = new Uri(AzureAI:Endpoint)`

### Requirement: Connector fails fast on unknown endpoint type
The system SHALL throw an `InvalidOperationException` with a descriptive message when `AzureAI:EndpointType` is set to an unrecognised value, so misconfiguration is caught at startup rather than at the first AI call.

#### Scenario: Unknown endpoint type
- **WHEN** `AzureAI:EndpointType` is set to an unrecognised value (e.g., `OpenAI`, `typo`)
- **THEN** startup fails with an error message that names the invalid value and lists valid options (`AzureOpenAI`, `AzureFoundry`)

### Requirement: Secrets use provider-neutral key names
The system SHALL read AI configuration from `AzureAI:Endpoint`, `AzureAI:ApiKey`, `AzureAI:DeploymentName`, and `AzureAI:EndpointType` (optional). The old `AzureOpenAI:*` keys SHALL no longer be read.

#### Scenario: Missing required secret
- **WHEN** any of `AzureAI:Endpoint`, `AzureAI:ApiKey`, or `AzureAI:DeploymentName` is absent from user secrets
- **THEN** startup fails immediately with an error message naming the missing key (e.g., `"AzureAI:Endpoint secret is missing"`)
