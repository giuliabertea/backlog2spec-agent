using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backlog2SpecAgent.Cli.Ado;

public sealed class ToolsApiHierarchyFetcher : IHierarchyFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public ToolsApiHierarchyFetcher(string baseUrl, string apiKey)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<WorkItemHierarchyDto> GetHierarchyAsync(int id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"{_baseUrl}/workitem/{id}/hierarchy", ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new AdoNotFoundException(id);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Tools API GET /workitem/{id}/hierarchy → {(int)resp.StatusCode}: {body}");

        var dto = JsonSerializer.Deserialize<HierarchyResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Tools API returned null hierarchy response.");

        return new WorkItemHierarchyDto(dto.Parent, dto.Children);
    }

    private sealed class HierarchyResponse
    {
        [JsonPropertyName("parent")]
        public WorkItemDto Parent { get; init; } = new();

        [JsonPropertyName("children")]
        public List<WorkItemDto> Children { get; init; } = [];
    }
}
