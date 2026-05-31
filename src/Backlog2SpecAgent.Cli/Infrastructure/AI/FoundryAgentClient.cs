using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Backlog2SpecAgent.Cli.Infrastructure.AI;

// Sends a single chat completion to a Foundry agent via the Responses API (no threads, no polling).
// TODO Phase 3: RAG — enrich the input with knowledge search results before calling.
public sealed class FoundryAgentClient : IFoundryAgentClient
{
    private const string ApiVersion = "2025-05-15-preview";

    private readonly string _baseUrl;
    private readonly string _agentId;
    private readonly TokenCredential _credential;
    private static readonly string[] TokenScopes = ["https://ai.azure.com/.default"];

    private readonly HttpClient _http;
    private readonly ILogger<FoundryAgentClient> _logger;

    public FoundryAgentClient(
        string projectEndpoint,
        string tenantId,
        string agentId,
        ILogger<FoundryAgentClient> logger)
    {
        _baseUrl = projectEndpoint.TrimEnd('/');
        _agentId = agentId;
        _credential = new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId });
        _http = new HttpClient();
        _logger = logger;

        _logger.LogWarning("FoundryAgentClient initialised — endpoint: {Endpoint}, agentId: {AgentId}",
            _baseUrl, _agentId);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext(TokenScopes), ct);
        return token.Token;
    }

    public async Task<string> RunAsync(string userMessage, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        var requestBody = JsonSerializer.Serialize(new { model = _agentId, input = userMessage });

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/responses?api-version={ApiVersion}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        _logger.LogDebug("Calling Foundry Responses API — agent: {AgentId}", _agentId);

        var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Foundry Responses API POST /responses → {(int)resp.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Convenience property present in the Responses API
        if (root.TryGetProperty("output_text", out var outputText))
            return outputText.GetString() ?? string.Empty;

        // Traverse the output array for message content
        var sb = new StringBuilder();
        if (root.TryGetProperty("output", out var output))
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "message") continue;
                if (!item.TryGetProperty("content", out var content)) continue;
                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                        sb.Append(text.GetString());
                }
            }
        }
        return sb.ToString();
    }
}
