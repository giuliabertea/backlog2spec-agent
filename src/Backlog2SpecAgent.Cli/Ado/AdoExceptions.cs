namespace Backlog2SpecAgent.Cli.Ado;

public sealed class AdoNotFoundException : Exception
{
    public int WorkItemId { get; }
    public AdoNotFoundException(int id) : base($"Work item {id} not found.") => WorkItemId = id;
}

public sealed class AdoAuthException : Exception
{
    public AdoAuthException(string message) : base(message) { }
    public AdoAuthException(string message, Exception inner) : base(message, inner) { }
}
