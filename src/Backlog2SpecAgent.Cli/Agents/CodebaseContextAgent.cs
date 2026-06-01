using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Config;
using Microsoft.Extensions.Logging;

namespace Backlog2SpecAgent.Cli.Agents;

public sealed class CodebaseContextAgent : ICodebaseContextAgent
{
    private const int MaxFiles = 8;
    private const int ContentMaxChars = 2000;
    private const int CandidateMultiplier = 5;

    private static readonly HashSet<string> SourceExtensions =
        [".cs", ".ts", ".js", ".py", ".java", ".go", ".md"];

    private readonly HttpClient _httpClient;
    private readonly IKeywordExtractor _keywordExtractor;
    private readonly ILogger<CodebaseContextAgent> _logger;

    public CodebaseContextAgent(string pat, IKeywordExtractor keywordExtractor, ILogger<CodebaseContextAgent> logger)
    {
        _keywordExtractor = keywordExtractor;
        _logger = logger;
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<IReadOnlyList<CodeFileDto>> FetchRelevantFilesAsync(
        WorkItemDto workItem, BacklogConfig config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.Ado.RepoName))
            return [];

        try
        {
            var branch = config.Ado.Branch ?? "master";
            var filePaths = await ListFilePathsAsync(config, branch, ct);
            var keywords = await _keywordExtractor.ExtractAsync(workItem, ct);

            _logger.LogDebug("Codebase search: [{Keywords}] against {Count} files",
                string.Join(", ", keywords), filePaths.Count);

            var candidatePaths = filePaths
                .Where(p => SourceExtensions.Contains(System.IO.Path.GetExtension(p).ToLowerInvariant()))
                .Select(p => (Path: p, Score: ScorePath(p, keywords)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Path.Length)
                .Take(MaxFiles * CandidateMultiplier)
                .Select(x => x.Path)
                .ToList();

            var candidates = new List<CodeFileDto>();
            foreach (var path in candidatePaths)
            {
                var file = await FetchFileContentAsync(config, branch, path, ct);
                if (file is not null) candidates.Add(file);
            }

            var results = candidates
                .Select(f => (File: f, Score: ScoreContent(f.Content, keywords)))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.File.Path.Length)
                .Take(MaxFiles)
                .Select(x => x.File)
                .ToList();

            _logger.LogInformation("Fetched {Count} source files for codebase context (from {Candidates} candidates)", results.Count, candidates.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Codebase context lookup failed — continuing without codebase context");
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> ListFilePathsAsync(
        BacklogConfig config, string branch, CancellationToken ct)
    {
        var url = $"{config.Ado.Organization}/{config.Ado.Project}/_apis/git/repositories/{config.Ado.RepoName}/items" +
                  $"?recursionLevel=full&versionDescriptor.version={Uri.EscapeDataString(branch)}&versionDescriptor.versionType=branch&api-version=7.1";

        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return [];

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var paths = new List<string>();

        if (!doc.RootElement.TryGetProperty("value", out var items)) return [];

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("gitObjectType", out var typeEl)) continue;
            if (typeEl.GetString() != "blob") continue;
            if (!item.TryGetProperty("path", out var pathEl)) continue;
            var path = pathEl.GetString();
            if (!string.IsNullOrEmpty(path))
                paths.Add(path);
        }

        return paths;
    }

    private async Task<CodeFileDto?> FetchFileContentAsync(
        BacklogConfig config, string branch, string filePath, CancellationToken ct)
    {
        var encodedPath = Uri.EscapeDataString(filePath);
        var url = $"{config.Ado.Organization}/{config.Ado.Project}/_apis/git/repositories/{config.Ado.RepoName}/items" +
                  $"?path={encodedPath}&includeContent=true&versionDescriptor.version={Uri.EscapeDataString(branch)}&versionDescriptor.versionType=branch&api-version=7.1";

        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var content = doc.RootElement.TryGetProperty("content", out var c)
            ? c.GetString() ?? string.Empty
            : string.Empty;

        if (content.Length > ContentMaxChars)
            content = content[..ContentMaxChars] + "...";

        return new CodeFileDto
        {
            Path = filePath,
            FileName = System.IO.Path.GetFileName(filePath),
            Content = content
        };
    }

    private static int ScorePath(string path, IReadOnlyList<string> keywords)
    {
        var lower = path.ToLowerInvariant();
        return keywords.Count(kw => lower.Contains(kw));
    }

    private static int ScoreContent(string content, IReadOnlyList<string> keywords)
    {
        var lower = content.ToLowerInvariant();
        return keywords.Sum(kw => lower.Split(kw).Length - 1);
    }
}
