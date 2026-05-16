using System.Text.Json.Serialization;

namespace Backlog2SpecAgent.Cli.Models;

public sealed class EnrichedTicket
{
    [JsonPropertyName("workItemId")]
    public int WorkItemId { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("missingAcceptanceCriteria")]
    public List<string> MissingAcceptanceCriteria { get; init; } = [];

    [JsonPropertyName("edgeCases")]
    public List<string> EdgeCases { get; init; } = [];

    [JsonPropertyName("constraints")]
    public List<string> Constraints { get; init; } = [];

    [JsonPropertyName("affectedComponents")]
    public List<string> AffectedComponents { get; init; } = [];

    [JsonPropertyName("ambiguities")]
    public List<string> Ambiguities { get; init; } = [];
}
