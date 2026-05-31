using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Backlog2SpecAgent.Cli.Infrastructure.AI;

// Wraps the Azure AI Foundry Agents REST API (2025-05-15-preview) via direct HttpClient calls.
// Tools (get_work_item, repo_context) are registered on the agent at first use
// and executed locally by forwarding HTTP calls to the Backlog2SpecAgent.Tools API.
// TODO Phase 3: RAG — trigger knowledge search before creating the thread.
public sealed class FoundryAgentClient : IFoundryAgentClient
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);
    private const string ApiVersion = "2025-05-15-preview";

    private readonly string _baseUrl;
    private readonly string _agentId;
    private readonly TokenCredential _credential;
    // Azure AI Foundry uses the ai.azure.com audience.
    private static readonly string[] TokenScopes = ["https://ai.azure.com/.default"];

    private readonly HttpClient _agentHttp;
    private readonly string _toolsBaseUrl;
    private readonly HttpClient _toolsHttp;
    private readonly ILogger<FoundryAgentClient> _logger;

    public FoundryAgentClient(
        string projectEndpoint,
        string tenantId,
        string agentName,
        string? agentId,
        string toolsBaseUrl,
        string toolsApiKey,
        ILogger<FoundryAgentClient> logger)
    {
        _baseUrl = projectEndpoint.TrimEnd('/');
        _agentId = !string.IsNullOrWhiteSpace(agentId) ? agentId : agentName;
        _credential = new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId });
        _toolsBaseUrl = toolsBaseUrl.TrimEnd('/');
        _logger = logger;

        _agentHttp = new HttpClient();
        _toolsHttp = new HttpClient();
        _toolsHttp.DefaultRequestHeaders.Add("X-Api-Key", toolsApiKey);

        _logger.LogWarning("FoundryAgentClient initialised — endpoint: {Endpoint}, agentId: {AgentId}",
            _baseUrl, _agentId);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext(TokenScopes), ct);
        return token.Token;
    }

    private async Task<HttpRequestMessage> BuildAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        var req = new HttpRequestMessage(method, $"{_baseUrl}/{path}?api-version={ApiVersion}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return req;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var resp = await _agentHttp.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Foundry API {req.Method} {req.RequestUri?.PathAndQuery} → {(int)resp.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    public async Task<string> RunAsync(string userMessage, CancellationToken ct = default)
    {
        // Create thread
        using var threadDoc = await SendAsync(
            await BuildAsync(HttpMethod.Post, "threads", new { }, ct), ct);
        var threadId = threadDoc.RootElement.GetProperty("id").GetString()!;
        _logger.LogDebug("Created thread {ThreadId}", threadId);

        try
        {
            // Add user message
            using var _ = await SendAsync(
                await BuildAsync(HttpMethod.Post, $"threads/{threadId}/messages",
                    new { role = "user", content = userMessage }, ct), ct);

            // Create run
            using var runDoc = await SendAsync(
                await BuildAsync(HttpMethod.Post, $"threads/{threadId}/runs",
                    new { agent_id = _agentId }, ct), ct);
            var runId = runDoc.RootElement.GetProperty("id").GetString()!;
            var status = runDoc.RootElement.GetProperty("status").GetString()!;
            _logger.LogDebug("Started run {RunId} on thread {ThreadId}", runId, threadId);

            status = await PollToCompletionAsync(threadId, runId, status, ct);

            if (status != "completed")
                throw new InvalidOperationException($"Agent run ended with status '{status}'");

            // Get messages (newest first)
            using var msgsDoc = await SendAsync(
                await BuildAsync(HttpMethod.Get, $"threads/{threadId}/messages", null, ct), ct);

            foreach (var msg in msgsDoc.RootElement.GetProperty("data").EnumerateArray())
            {
                if (msg.GetProperty("role").GetString() != "assistant") continue;
                var sb = new StringBuilder();
                foreach (var part in msg.GetProperty("content").EnumerateArray())
                {
                    if (part.GetProperty("type").GetString() != "text") continue;
                    sb.Append(part.GetProperty("text").GetProperty("value").GetString());
                }
                return sb.ToString();
            }

            return string.Empty;
        }
        finally
        {
            try
            {
                var del = await BuildAsync(HttpMethod.Delete, $"threads/{threadId}", null, CancellationToken.None);
                await _agentHttp.SendAsync(del, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete thread {ThreadId}", threadId);
            }
        }
    }

    private async Task<string> PollToCompletionAsync(
        string threadId, string runId, string status, CancellationToken ct)
    {
        while (true)
        {
            switch (status)
            {
                case "completed":
                case "failed":
                case "cancelled":
                case "expired":
                    return status;

                case "requires_action":
                    status = await HandleToolCallsAsync(threadId, runId, ct);
                    break;

                default: // queued, in_progress
                    await Task.Delay(PollingInterval, ct);
                    using (var runDoc = await SendAsync(
                        await BuildAsync(HttpMethod.Get, $"threads/{threadId}/runs/{runId}", null, ct), ct))
                    {
                        status = runDoc.RootElement.GetProperty("status").GetString()!;
                    }
                    break;
            }
        }
    }

    private async Task<string> HandleToolCallsAsync(string threadId, string runId, CancellationToken ct)
    {
        using var runDoc = await SendAsync(
            await BuildAsync(HttpMethod.Get, $"threads/{threadId}/runs/{runId}", null, ct), ct);

        var toolCalls = runDoc.RootElement
            .GetProperty("required_action")
            .GetProperty("submit_tool_outputs")
            .GetProperty("tool_calls");

        var outputs = new List<object>();
        foreach (var call in toolCalls.EnumerateArray())
        {
            var callId = call.GetProperty("id").GetString()!;
            var fn = call.GetProperty("function");
            var name = fn.GetProperty("name").GetString()!;
            var arguments = fn.GetProperty("arguments").GetString()!;
            _logger.LogDebug("Executing tool '{ToolName}' with args: {Args}", name, arguments);
            var result = await ExecuteToolAsync(name, arguments, ct);
            outputs.Add(new { tool_call_id = callId, output = result });
        }

        using var submitDoc = await SendAsync(
            await BuildAsync(HttpMethod.Post, $"threads/{threadId}/runs/{runId}/submit_tool_outputs",
                new { tool_outputs = outputs }, ct), ct);
        return submitDoc.RootElement.GetProperty("status").GetString()!;
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
