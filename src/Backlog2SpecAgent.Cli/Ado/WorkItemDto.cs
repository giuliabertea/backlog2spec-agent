using System.Text.Json.Serialization;

namespace Backlog2SpecAgent.Cli.Ado;

public sealed class WorkItemDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("workItemType")]
    public string WorkItemType { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("acceptanceCriteria")]
    public string AcceptanceCriteria { get; init; } = string.Empty;
}
