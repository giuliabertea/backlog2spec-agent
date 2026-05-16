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
    private readonly string _agentId;
    private readonly ILogger<FoundryAgentClient> _logger;

    public FoundryAgentClient(string endpoint, string apiKey, string agentId, ILogger<FoundryAgentClient> logger)
    {
        _client = new AssistantsClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _agentId = agentId;
        _logger = logger;
    }

    public async Task<string> RunAsync(string userMessage, CancellationToken ct = default)
    {
        // TODO Phase 3: RAG — trigger knowledge search over the codebase index here before creating the thread
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
                new CreateRunOptions(_agentId),
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
