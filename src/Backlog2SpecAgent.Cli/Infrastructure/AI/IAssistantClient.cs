namespace Backlog2SpecAgent.Cli.Infrastructure.AI;

public interface IAssistantClient
{
    Task<string> RunAsync(string userMessage, CancellationToken ct = default);
}
