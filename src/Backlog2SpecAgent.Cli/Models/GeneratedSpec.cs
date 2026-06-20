using System.Text.Json.Serialization;

namespace Backlog2SpecAgent.Cli.Models;

public sealed class GeneratedSpec
{
    [JsonPropertyName("goal")]
    public string Goal { get; init; } = string.Empty;

    [JsonPropertyName("behaviour")]
    public List<string> Behaviour { get; init; } = [];

    [JsonPropertyName("edgeCases")]
    public List<string> EdgeCases { get; init; } = [];

    [JsonPropertyName("outOfScope")]
    public string OutOfScope { get; init; } = string.Empty;

    [JsonPropertyName("filesToChange")]
    public List<FileChange> FilesToChange { get; init; } = [];

    [JsonPropertyName("openQuestions")]
    public List<string> OpenQuestions { get; init; } = [];

    [JsonPropertyName("conventions")]
    public List<string> Conventions { get; init; } = [];
}

public sealed class FileChange
{
    [JsonPropertyName("file")]
    public string File { get; init; } = string.Empty;

    [JsonPropertyName("change")]
    public string Change { get; init; } = string.Empty;

    [JsonPropertyName("evidence")]
    public string Evidence { get; init; } = string.Empty;

    [JsonPropertyName("confidence")]
    public string Confidence { get; init; } = string.Empty;
}
