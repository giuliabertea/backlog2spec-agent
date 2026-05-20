using Azure;
using Azure.AI.OpenAI.Assistants;
using Microsoft.Extensions.Logging;

namespace Backlog2SpecAgent.Cli.Infrastructure.AI;

// Wraps the Azure AI Foundry Assistants API (thread → message → run → poll → extract).
// The agent's system prompt and model are configured in the Azure AI Foundry portal.
public sealed class FoundryAgentClient : IFoundryAgentClient
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);

    private readonly AssistantsClient _client;
    private readonly string _agentName;
    private readonly ILogger<FoundryAgentClient> _logger;
    // Resolved once on first use and cached for the lifetime of this singleton.
    private readonly Lazy<Task<string>> _agentIdResolver;

    public FoundryAgentClient(string endpoint, string apiKey, string agentName, ILogger<FoundryAgentClient> logger)
    {
        _client = new AssistantsClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _agentName = agentName;
        _logger = logger;
        _agentIdResolver = new Lazy<Task<string>>(() => ResolveAgentIdAsync(agentName));
    }

    private async Task<string> ResolveAgentIdAsync(string agentName)
    {
        _logger.LogDebug("Resolving agent ID for name '{AgentName}'", agentName);
        var assistants = (await _client.GetAssistantsAsync()).Value;
        var match = assistants.Data.FirstOrDefault(a => a.Name == agentName);
        if (match is null)
            throw new InvalidOperationException(
                $"Agent '{agentName}' not found. Check AzureAI:AgentName in config.");
        _logger.LogDebug("Resolved agent '{AgentName}' to ID {AgentId}", agentName, match.Id);
        return match.Id;
    }

    public async Task<string> RunAsync(string userMessage, CancellationToken ct = default)
    {
        // TODO Phase 3: RAG — trigger knowledge search over the codebase index here before creating the thread
        var agentId = await _agentIdResolver.Value;
        var thread = (await _client.CreateThreadAsync(ct)).Value;
        _logger.LogDebug("Created agent thread {ThreadId}", thread.Id);

        try
        {
            await _client.CreateMessageAsync(
                thread.Id,
                MessageRole.User,
                userMessage,
                cancellationToken: ct);

            var run = (await _client.CreateRunAsync(
                thread.Id,
                new CreateRunOptions(agentId),
                ct)).Value;

            _logger.LogDebug("Started agent run {RunId} on thread {ThreadId}", run.Id, thread.Id);

            while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
            {
                await Task.Delay(PollingInterval, ct);
                run = (await _client.GetRunAsync(thread.Id, run.Id, ct)).Value;
            }

            if (run.Status != RunStatus.Completed)
                throw new InvalidOperationException(
                    $"Agent run ended with status '{run.Status}': {run.LastError?.Message}");

            var messages = (await _client.GetMessagesAsync(thread.Id, cancellationToken: ct)).Value;
            var lastAssistantMessage = messages.Data
                .LastOrDefault(m => m.Role == MessageRole.Assistant);

            return string.Concat(
                lastAssistantMessage?.ContentItems
                    .OfType<MessageTextContent>()
                    .Select(c => c.Text) ?? []);
        }
        finally
        {
            try
            {
                await _client.DeleteThreadAsync(thread.Id, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete thread {ThreadId} — it will expire automatically", thread.Id);
            }
        }
    }
}
