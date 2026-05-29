using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI.Assistants;
using Microsoft.Extensions.Logging;

namespace Backlog2SpecAgent.Cli.Infrastructure.AI;

// Wraps the Azure OpenAI Assistants API (via Azure AI Foundry).
// Tools (get_work_item, repo_context) are registered on the agent at first use
// and executed locally by forwarding HTTP calls to the Backlog2SpecAgent.Tools API.
// TODO Phase 3: RAG — trigger knowledge search before creating the thread.
public sealed class FoundryAgentClient : IFoundryAgentClient
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);

    private static readonly BinaryData GetWorkItemParameters = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "id": { "type": "string", "description": "Azure DevOps work item ID" }
            },
            "required": ["id"]
        }
        """);

    private static readonly BinaryData RepoContextParameters = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "query": { "type": "string", "description": "Search query to find relevant source code files" }
            },
            "required": ["query"]
        }
        """);

    private readonly AssistantsClient _client;
    private readonly string _toolsBaseUrl;
    private readonly HttpClient _toolsHttp;
    private readonly ILogger<FoundryAgentClient> _logger;
    // Resolved once on first use and cached for the lifetime of this singleton.
    private readonly Lazy<Task<string>> _agentIdResolver;

    public FoundryAgentClient(
        string endpoint,
        string apiKey,
        string agentName,
        string toolsBaseUrl,
        string toolsApiKey,
        ILogger<FoundryAgentClient> logger)
    {
        _client = new AssistantsClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _toolsBaseUrl = toolsBaseUrl.TrimEnd('/');
        _logger = logger;

        _toolsHttp = new HttpClient();
        _toolsHttp.DefaultRequestHeaders.Add("X-Api-Key", toolsApiKey);

        _agentIdResolver = new Lazy<Task<string>>(() => ResolveAndConfigureAgentAsync(agentName));
    }

    private async Task<string> ResolveAndConfigureAgentAsync(string agentName)
    {
        _logger.LogDebug("Resolving agent '{AgentName}' and registering tools", agentName);

        var tools = new List<ToolDefinition>
        {
            new FunctionToolDefinition(
                name: "get_work_item",
                description: "Fetches an Azure DevOps work item by ID and returns its title, type, description, and acceptance criteria as JSON.",
                parameters: GetWorkItemParameters),
            new FunctionToolDefinition(
                name: "repo_context",
                description: "Fetches relevant source code file snippets from the repository for a given search query. Returns a list of { path, content } objects.",
                parameters: RepoContextParameters)
        };

        var assistantsPage = (await _client.GetAssistantsAsync(cancellationToken: CancellationToken.None)).Value;
        foreach (var assistant in assistantsPage.Data)
        {
            if (assistant.Name != agentName) continue;

            var updateOptions = new UpdateAssistantOptions();
            foreach (var tool in tools) updateOptions.Tools.Add(tool);
            await _client.UpdateAssistantAsync(assistant.Id, updateOptions, CancellationToken.None);
            _logger.LogDebug("Registered tools on agent '{AgentName}' (id={AgentId})", agentName, assistant.Id);
            return assistant.Id;
        }

        throw new InvalidOperationException(
            $"Agent '{agentName}' not found in Azure AI Foundry. Check AzureAI:AgentName in config.");
    }

    public async Task<string> RunAsync(string userMessage, CancellationToken ct = default)
    {
        var agentId = await _agentIdResolver.Value;

        var thread = (await _client.CreateThreadAsync(ct)).Value;
        _logger.LogDebug("Created thread {ThreadId}", thread.Id);

        try
        {
            // TODO Phase 3: RAG — query Azure AI Search (see scripts/index-repo.ps1) with
            // keywords extracted from userMessage; prepend top-k matching code snippets as
            // additional context before the user message so the agent has grounded context.
            await _client.CreateMessageAsync(thread.Id, MessageRole.User, userMessage, cancellationToken: ct);

            var run = (await _client.CreateRunAsync(thread.Id, new CreateRunOptions(agentId), ct)).Value;
            _logger.LogDebug("Started run {RunId} on thread {ThreadId}", run.Id, thread.Id);

            run = await PollToCompletionAsync(thread.Id, run, ct);

            if (run.Status != RunStatus.Completed)
                throw new InvalidOperationException(
                    $"Agent run ended with status '{run.Status}': {run.LastError?.Message}");

            var messagesResponse = await _client.GetMessagesAsync(thread.Id, cancellationToken: ct);
            var textParts = new List<string>();
            foreach (var msg in messagesResponse.Value.Data)
            {
                if (msg.Role != MessageRole.Assistant) continue;
                foreach (var item in msg.ContentItems.OfType<MessageTextContent>())
                    textParts.Add(item.Text);
                break; // first (most-recent) assistant message
            }
            return string.Concat(textParts);
        }
        finally
        {
            try { await _client.DeleteThreadAsync(thread.Id, CancellationToken.None); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete thread {ThreadId}", thread.Id);
            }
        }
    }

    private async Task<ThreadRun> PollToCompletionAsync(string threadId, ThreadRun run, CancellationToken ct)
    {
        while (true)
        {
            switch (run.Status.ToString())
            {
                case "completed":
                case "failed":
                case "cancelled":
                case "expired":
                    return run;

                case "requires_action":
                    run = await HandleToolCallsAsync(threadId, run, ct);
                    break;

                default: // queued, in_progress
                    await Task.Delay(PollingInterval, ct);
                    run = (await _client.GetRunAsync(threadId, run.Id, ct)).Value;
                    break;
            }
        }
    }

    private async Task<ThreadRun> HandleToolCallsAsync(string threadId, ThreadRun run, CancellationToken ct)
    {
        if (run.RequiredAction is not SubmitToolOutputsAction submitAction)
            throw new InvalidOperationException($"Unexpected required action type: {run.RequiredAction?.GetType().Name}");

        var outputs = new List<ToolOutput>();
        foreach (var call in submitAction.ToolCalls)
        {
            if (call is not RequiredFunctionToolCall fn) continue;
            _logger.LogDebug("Executing tool '{ToolName}' with args: {Args}", fn.Name, fn.Arguments);
            var result = await ExecuteToolAsync(fn.Name, fn.Arguments, ct);
            outputs.Add(new ToolOutput(fn.Id, result));
        }

        return (await _client.SubmitToolOutputsToRunAsync(threadId, run.Id, outputs, ct)).Value;
    }

    private async Task<string> ExecuteToolAsync(string toolName, string argumentsJson, CancellationToken ct)
    {
        try
        {
            using var args = JsonDocument.Parse(argumentsJson);

            return toolName switch
            {
                "get_work_item" => await CallGetWorkItemAsync(args.RootElement, ct),
                "repo_context"  => await CallRepoContextAsync(args.RootElement, ct),
                _               => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tool '{ToolName}' execution failed", toolName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> CallGetWorkItemAsync(JsonElement args, CancellationToken ct)
    {
        var id = args.GetProperty("id").GetString();
        var response = await _toolsHttp.GetAsync($"{_toolsBaseUrl}/workitem/{id}", ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<string> CallRepoContextAsync(JsonElement args, CancellationToken ct)
    {
        var query = args.GetProperty("query").GetString() ?? string.Empty;
        var body = JsonSerializer.Serialize(new { query });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _toolsHttp.PostAsync($"{_toolsBaseUrl}/repo-context", content, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }
}
