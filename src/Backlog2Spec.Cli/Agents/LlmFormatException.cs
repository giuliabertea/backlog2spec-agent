namespace Backlog2SpecAgent.Cli.Agents;

public sealed class LlmFormatException : Exception
{
    public string RawResponse { get; }

    public LlmFormatException(string rawResponse, Exception? inner = null)
        : base($"LLM returned invalid JSON after all retry attempts. Raw response length: {rawResponse.Length} chars.", inner)
    {
        RawResponse = rawResponse;
    }
}
