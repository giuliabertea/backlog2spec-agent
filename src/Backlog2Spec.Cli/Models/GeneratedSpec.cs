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
    public List<string> FilesToChange { get; init; } = [];
}
