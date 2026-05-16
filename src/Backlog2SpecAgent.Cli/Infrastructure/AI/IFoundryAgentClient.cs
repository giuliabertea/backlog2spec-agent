namespace Backlog2SpecAgent.Cli.Infrastructure.AI;

public interface IFoundryAgentClient
{
    Task<string> RunAsync(string userMessage, CancellationToken ct = default);
}
