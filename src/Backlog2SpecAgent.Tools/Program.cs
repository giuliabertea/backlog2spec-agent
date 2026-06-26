using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Backlog2Spec Tools API",
        Version = "v1",
        Description = "Repository navigation and work item tools for the Backlog2SpecAgent Foundry agent."
    });
    var baseUrl = builder.Configuration["Tools:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
        c.AddServer(new OpenApiServer { Url = baseUrl });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Backlog2Spec Tools API v1"));

// Authentication: not yet implemented. Planned: Managed Identity on the Foundry agent connection.

// ── GET /workitem/{id} ──────────────────────────────────────────────────────
app.MapGet("/workitem/{id:int}", async (int id, IConfiguration config, IHttpClientFactory factory) =>
{
    var (org, project, pat, err) = GetAdoBase(config);
    if (err is not null) return Results.Problem(err);

    using var http = CreateAdoHttp(factory, pat!);
    var url = $"{org!.TrimEnd('/')}/{project}/_apis/wit/workitems/{id}?$expand=all&api-version=7.1";
    var response = await http.GetAsync(url);

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        return Results.NotFound(new { error = $"Work item {id} not found." });
    if (!response.IsSuccessStatusCode)
        return Results.Problem($"ADO returned {(int)response.StatusCode}: {response.ReasonPhrase}");

    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var fields = doc.RootElement.GetProperty("fields");

    return Results.Ok(new
    {
        id,
        title              = GetField(fields, "System.Title"),
        workItemType       = GetField(fields, "System.WorkItemType"),
        description        = StripHtml(GetField(fields, "System.Description")),
        acceptanceCriteria = StripHtml(GetField(fields, "Microsoft.VSTS.Common.AcceptanceCriteria"))
    });
})
.WithName("getWorkItem")
.WithSummary("Get a single work item")
.WithDescription("Fetches the title, description, acceptance criteria, and type of an Azure DevOps work item by ID.");

// ── GET /workitem/{id}/hierarchy ────────────────────────────────────────────
app.MapGet("/workitem/{id:int}/hierarchy", async (int id, IConfiguration config, IHttpClientFactory factory) =>
{
    var (org, project, pat, err) = GetAdoBase(config);
    if (err is not null) return Results.Problem(err);

    using var http = CreateAdoHttp(factory, pat!);

    // 1. Fetch parent with relations expanded
    var parentUrl = $"{org!.TrimEnd('/')}/{project}/_apis/wit/workitems/{id}?$expand=relations&api-version=7.1";
    var parentResp = await http.GetAsync(parentUrl);

    if (parentResp.StatusCode == System.Net.HttpStatusCode.NotFound)
        return Results.NotFound(new { error = $"Work item {id} not found." });
    if (!parentResp.IsSuccessStatusCode)
        return Results.Problem($"ADO returned {(int)parentResp.StatusCode}: {parentResp.ReasonPhrase}");

    using var parentDoc = JsonDocument.Parse(await parentResp.Content.ReadAsStringAsync());
    var parentFields = parentDoc.RootElement.GetProperty("fields");

    var parentDto = new
    {
        id,
        title              = GetField(parentFields, "System.Title"),
        workItemType       = GetField(parentFields, "System.WorkItemType"),
        description        = StripHtml(GetField(parentFields, "System.Description")),
        acceptanceCriteria = StripHtml(GetField(parentFields, "Microsoft.VSTS.Common.AcceptanceCriteria"))
    };

    // 2. Extract child IDs from Hierarchy-Forward relations
    var childIds = new List<int>();
    if (parentDoc.RootElement.TryGetProperty("relations", out var relations))
    {
        foreach (var rel in relations.EnumerateArray())
        {
            if (!rel.TryGetProperty("rel", out var relType) ||
                relType.GetString() != "System.LinkTypes.Hierarchy-Forward") continue;
            if (!rel.TryGetProperty("url", out var urlProp)) continue;
            var relUrl = urlProp.GetString();
            if (string.IsNullOrEmpty(relUrl)) continue;
            var lastSegment = relUrl.Split('/').LastOrDefault();
            if (int.TryParse(lastSegment, out var childId))
                childIds.Add(childId);
        }
    }

    // 3. Bulk-fetch children if any
    var childrenDtos = new List<object>();
    if (childIds.Count > 0)
    {
        var idsParam = string.Join(",", childIds);
        var childrenUrl = $"{org.TrimEnd('/')}/{project}/_apis/wit/workitems?ids={idsParam}&$expand=all&api-version=7.1";
        var childrenResp = await http.GetAsync(childrenUrl);

        if (childrenResp.IsSuccessStatusCode)
        {
            using var childrenDoc = JsonDocument.Parse(await childrenResp.Content.ReadAsStringAsync());
            if (childrenDoc.RootElement.TryGetProperty("value", out var childItems))
            {
                foreach (var child in childItems.EnumerateArray())
                {
                    var childId = child.TryGetProperty("id", out var cid) ? cid.GetInt32() : 0;
                    var childFields = child.GetProperty("fields");
                    childrenDtos.Add(new
                    {
                        id = childId,
                        title              = GetField(childFields, "System.Title"),
                        workItemType       = GetField(childFields, "System.WorkItemType"),
                        description        = StripHtml(GetField(childFields, "System.Description")),
                        acceptanceCriteria = StripHtml(GetField(childFields, "Microsoft.VSTS.Common.AcceptanceCriteria"))
                    });
                }
            }
        }
    }

    return Results.Ok(new { parent = parentDto, children = childrenDtos });
})
.WithName("getWorkItemHierarchy")
.WithSummary("Get a work item with its children")
.WithDescription("Fetches a parent work item (Feature or Epic) and all its direct child work items in one call.");

// ── POST /repo-context ──────────────────────────────────────────────────────
app.MapPost("/repo-context", async (RepoContextRequest req, IConfiguration config, IHttpClientFactory factory) =>
{
    var (org, project, pat, err) = GetAdoBase(config);
    if (err is not null) return Results.Problem(err);

    var repo   = config["Ado:RepoName"];
    var branch = config["Ado:Branch"] ?? "main";

    if (string.IsNullOrEmpty(repo))
        return Results.Ok(Array.Empty<object>());

    using var http = CreateAdoHttp(factory, pat!);

    // List all file paths in the repo
    var listUrl = $"{org!.TrimEnd('/')}/{project}/_apis/git/repositories/{repo}/items" +
                  $"?recursionLevel=full&versionDescriptor.version={Uri.EscapeDataString(branch)}" +
                  $"&versionDescriptor.versionType=branch&api-version=7.1";

    var listResp = await http.GetAsync(listUrl);
    if (!listResp.IsSuccessStatusCode)
    {
        var body = await listResp.Content.ReadAsStringAsync();
        return Results.Problem($"ADO error {(int)listResp.StatusCode}: {body}");
    }

    var allPaths = new List<string>();
    using (var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync()))
    {
        if (listDoc.RootElement.TryGetProperty("value", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("gitObjectType", out var t) || t.GetString() != "blob") continue;
                if (!item.TryGetProperty("path", out var p)) continue;
                var path = p.GetString();
                if (!string.IsNullOrEmpty(path)) allPaths.Add(path);
            }
        }
    }

    var sourceExts = new HashSet<string> { ".cs", ".ts", ".js", ".py", ".java", ".go", ".md" };
    var keywords   = ExtractKeywords(req.Query);

    var candidates = allPaths
        .Where(p => sourceExts.Contains(Path.GetExtension(p).ToLowerInvariant()))
        .Select(p => (Path: p, Score: keywords.Count(kw => p.ToLowerInvariant().Contains(kw))))
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(40)
        .Select(x => x.Path)
        .ToList();

    const int maxFiles = 8, maxChars = 2000;
    var results = new List<object>();

    foreach (var filePath in candidates)
    {
        if (results.Count >= maxFiles) break;
        var content = await FetchFileContentAsync(http, org!, project!, repo, branch, filePath);
        if (content is null) continue;
        if (content.Length > maxChars) content = content[..maxChars] + "...";
        results.Add(new { path = filePath, content });
    }

    return Results.Ok(results);
})
.WithName("searchCode")
.WithSummary("Search code by keyword")
.WithDescription("Returns source file snippets from the ADO repository whose paths match keywords extracted from the query string. Use this for broad discovery when you don't know exact file paths.");

// ── POST /spec ──────────────────────────────────────────────────────────────
app.MapPost("/spec", async (SaveSpecRequest req, IConfiguration config) =>
{
    var dir = config["SpecOutput:Directory"] ?? "./specs";
    Directory.CreateDirectory(dir);
    var filePath = Path.Combine(dir, $"spec-{req.WorkItemId}.md");
    await File.WriteAllTextAsync(filePath, req.Content, Encoding.UTF8);
    return Results.Ok(new { saved = filePath });
})
.WithName("saveSpec")
.WithSummary("Save a generated spec to disk")
.WithDescription("Writes the provided spec content to a markdown file named spec-{workItemId}.md in the configured output directory.");

// ── GET /repo/tree ──────────────────────────────────────────────────────────
app.MapGet("/repo/tree", async (string? path, IConfiguration config, IHttpClientFactory factory) =>
{
    var (org, project, pat, err) = GetAdoBase(config);
    if (err is not null) return Results.Problem(err);

    var repo   = config["Ado:RepoName"];
    var branch = config["Ado:Branch"] ?? "main";

    if (string.IsNullOrEmpty(repo))
        return Results.Ok(Array.Empty<object>());

    var dirPath = string.IsNullOrWhiteSpace(path) ? "/" : path;

    using var http = CreateAdoHttp(factory, pat!);
    var encoded = Uri.EscapeDataString(dirPath);
    var listUrl = $"{org!.TrimEnd('/')}/{project}/_apis/git/repositories/{repo}/items" +
                  $"?scopePath={encoded}&recursionLevel=OneLevel" +
                  $"&versionDescriptor.version={Uri.EscapeDataString(branch)}" +
                  $"&versionDescriptor.versionType=branch&api-version=7.1";

    var resp = await http.GetAsync(listUrl);
    if (!resp.IsSuccessStatusCode)
    {
        var errorBody = await resp.Content.ReadAsStringAsync();
        return Results.Problem($"ADO error {(int)resp.StatusCode}: {errorBody}");
    }

    var entries = new List<object>();
    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    if (doc.RootElement.TryGetProperty("value", out var items))
    {
        foreach (var item in items.EnumerateArray())
        {
            var itemPath = item.TryGetProperty("path", out var p) ? p.GetString() : null;
            var objType  = item.TryGetProperty("gitObjectType", out var t) ? t.GetString() : null;

            // Skip the directory entry itself
            if (itemPath is null || itemPath == dirPath) continue;

            var type = objType == "tree" ? "folder" : "file";
            entries.Add(new { path = itemPath, type });
        }
    }

    return Results.Ok(entries);
})
.WithName("listDirectory")
.WithSummary("List files and folders in a directory")
.WithDescription("Lists the immediate children (one level deep) of the specified directory path in the ADO repository. Returns each entry with its path and type (\"file\" or \"folder\"). Use this to navigate the repo structure before reading specific files.");

// ── GET /repo/file ──────────────────────────────────────────────────────────
app.MapGet("/repo/file", async (string path, int? startLine, int? endLine, IConfiguration config, IHttpClientFactory factory) =>
{
    var (org, project, pat, err) = GetAdoBase(config);
    if (err is not null) return Results.Problem(err);

    var repo   = config["Ado:RepoName"];
    var branch = config["Ado:Branch"] ?? "main";

    if (string.IsNullOrEmpty(repo))
        return Results.NotFound(new { error = "Repository not configured." });

    using var http = CreateAdoHttp(factory, pat!);
    var content = await FetchFileContentAsync(http, org!, project!, repo, branch, path);

    if (content is null)
        return Results.NotFound(new { error = $"File not found: {path}" });

    var lines = content.Split('\n');
    var totalLines = lines.Length;

    var start = Math.Max(1, startLine ?? 1);
    var end   = Math.Min(totalLines, endLine ?? totalLines);
    end = Math.Min(end, start + 399); // cap at 400 lines

    var sb = new StringBuilder();
    for (int i = start; i <= end; i++)
    {
        var lineContent = lines[i - 1].TrimEnd('\r');
        sb.AppendLine($"{i}: {lineContent}");
    }

    return Results.Ok(new
    {
        path,
        startLine  = start,
        endLine    = end,
        totalLines,
        content    = sb.ToString()
    });
})
.WithName("readFile")
.WithSummary("Read a file by line range")
.WithDescription("Returns the content of a source file for the specified line range (max 400 lines), with each line prefixed by its line number (e.g. \"42: public void Foo()\"). Use getFileOutline first to identify which lines are interesting, then read those specific sections.");

// ── POST /repo/references ───────────────────────────────────────────────────
app.MapPost("/repo/references", async (FindReferencesRequest req, IConfiguration config, IHttpClientFactory factory) =>
{
    if (string.IsNullOrWhiteSpace(req.Symbol))
        return Results.BadRequest(new { error = "symbol must not be empty." });

    var (org, project, pat, err) = GetAdoBase(config);
    if (err is not null) return Results.Problem(err);

    var repo   = config["Ado:RepoName"];
    var branch = config["Ado:Branch"] ?? "main";

    if (string.IsNullOrEmpty(repo))
        return Results.Ok(Array.Empty<object>());

    using var http = CreateAdoHttp(factory, pat!);

    // 1. Get all file paths
    var listUrl = $"{org!.TrimEnd('/')}/{project}/_apis/git/repositories/{repo}/items" +
                  $"?recursionLevel=full&versionDescriptor.version={Uri.EscapeDataString(branch)}" +
                  $"&versionDescriptor.versionType=branch&api-version=7.1";

    var listResp = await http.GetAsync(listUrl);
    if (!listResp.IsSuccessStatusCode)
    {
        var body = await listResp.Content.ReadAsStringAsync();
        return Results.Problem($"ADO error {(int)listResp.StatusCode}: {body}");
    }

    var sourceExts = new HashSet<string> { ".cs", ".ts", ".js", ".py", ".java", ".go" };
    var sourcePaths = new List<string>();

    using (var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync()))
    {
        if (listDoc.RootElement.TryGetProperty("value", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("gitObjectType", out var t) || t.GetString() != "blob") continue;
                if (!item.TryGetProperty("path", out var p)) continue;
                var filePath = p.GetString();
                if (!string.IsNullOrEmpty(filePath) && sourceExts.Contains(Path.GetExtension(filePath).ToLowerInvariant()))
                    sourcePaths.Add(filePath);
            }
        }
    }

    // 2. Search each file for whole-word occurrences (stop at 50 results or 200 files)
    var pattern = new Regex($@"\b{Regex.Escape(req.Symbol)}\b", RegexOptions.None);
    var matches = new List<object>();
    var filesChecked = 0;

    foreach (var filePath in sourcePaths)
    {
        if (matches.Count >= 50 || filesChecked >= 200) break;
        filesChecked++;

        var content = await FetchFileContentAsync(http, org, project!, repo, branch, filePath);
        if (content is null) continue;

        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (matches.Count >= 50) break;
            var line = lines[i].TrimEnd('\r');
            if (pattern.IsMatch(line))
                matches.Add(new { path = filePath, line = i + 1, snippet = line.Trim() });
        }
    }

    return Results.Ok(matches);
})
.WithName("findReferences")
.WithSummary("Find all references to a symbol")
.WithDescription("Performs a whole-word case-sensitive search for the given symbol name across all source files (.cs, .ts, .js, .py, .java, .go) in the repository. Returns up to 50 matches, each with the file path, line number, and the matching line content. Useful for finding callers, interface implementations, and usages.");

// ── GET /repo/outline ───────────────────────────────────────────────────────
app.MapGet("/repo/outline", async (string path, IConfiguration config, IHttpClientFactory factory) =>
{
    var (org, project, pat, err) = GetAdoBase(config);
    if (err is not null) return Results.Problem(err);

    var repo   = config["Ado:RepoName"];
    var branch = config["Ado:Branch"] ?? "main";

    if (string.IsNullOrEmpty(repo))
        return Results.NotFound(new { error = "Repository not configured." });

    using var http = CreateAdoHttp(factory, pat!);
    var content = await FetchFileContentAsync(http, org!, project!, repo, branch, path);

    if (content is null)
        return Results.NotFound(new { error = $"File not found: {path}" });

    var ext = Path.GetExtension(path).ToLowerInvariant();
    if (ext != ".cs")
        return Results.Ok(new { path, note = "Outline extraction is only supported for .cs files.", symbols = Array.Empty<object>() });

    var symbols = ExtractCSharpSymbols(content);
    return Results.Ok(new { path, symbols });
})
.WithName("getFileOutline")
.WithSummary("Get the structural outline of a source file")
.WithDescription("Extracts namespaces, types (class/record/interface/struct), and public method signatures with line numbers from a .cs file. Use this to orient yourself in a large file before calling readFile to read specific sections. Returns an empty symbol list with a note for non-.cs files.");

app.Run();

// ── ADO helpers ──────────────────────────────────────────────────────────────
static (string? org, string? project, string? pat, string? error) GetAdoBase(IConfiguration config)
{
    var org     = config["Ado:Organization"];
    var project = config["Ado:Project"];
    var pat     = config["Ado:Pat"];

    if (string.IsNullOrEmpty(org) || string.IsNullOrEmpty(project) || string.IsNullOrEmpty(pat))
        return (null, null, null, "ADO configuration incomplete. Set Ado:Organization, Ado:Project, and Ado:Pat.");

    return (org, project, pat, null);
}

static HttpClient CreateAdoHttp(IHttpClientFactory factory, string pat)
{
    var http = factory.CreateClient();
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));
    return http;
}

static async Task<string?> FetchFileContentAsync(HttpClient http, string org, string project, string repo, string branch, string filePath)
{
    var encoded = Uri.EscapeDataString(filePath.TrimStart('/'));
    var fileUrl = $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repo}/items" +
                  $"?path={encoded}&includeContent=true" +
                  $"&versionDescriptor.version={Uri.EscapeDataString(branch)}" +
                  $"&versionDescriptor.versionType=branch&api-version=7.1";

    var resp = await http.GetAsync(fileUrl);
    if (!resp.IsSuccessStatusCode) return null;

    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    return doc.RootElement.TryGetProperty("content", out var c) ? c.GetString() : null;
}

static List<object> ExtractCSharpSymbols(string content)
{
    var symbols = new List<object>();
    var lines = content.Split('\n');

    var nsRegex     = new Regex(@"^\s*namespace\s+([\w.]+)", RegexOptions.Compiled);
    var typeRegex   = new Regex(@"(?:public|internal|private|protected)(?:\s+(?:sealed|abstract|static|partial))*\s+(class|record|interface|struct)\s+(\w+)", RegexOptions.Compiled);
    var methodRegex = new Regex(@"^\s*(?:public|internal|protected)(?:\s+(?:static|async|virtual|override|abstract|sealed|new))*\s+\S+\s+(\w+)\s*[(<]", RegexOptions.Compiled);
    var propRegex   = new Regex(@"\{.*\bget\b", RegexOptions.Compiled);

    for (int i = 0; i < lines.Length; i++)
    {
        var line    = lines[i].TrimEnd('\r');
        var lineNum = i + 1;

        var nsMatch = nsRegex.Match(line);
        if (nsMatch.Success)
        {
            symbols.Add(new { kind = "namespace", name = nsMatch.Groups[1].Value, signature = line.Trim(), line = lineNum });
            continue;
        }

        var typeMatch = typeRegex.Match(line);
        if (typeMatch.Success)
        {
            symbols.Add(new { kind = typeMatch.Groups[1].Value, name = typeMatch.Groups[2].Value, signature = line.Trim(), line = lineNum });
            continue;
        }

        var methodMatch = methodRegex.Match(line);
        if (methodMatch.Success && !propRegex.IsMatch(line) && !line.TrimStart().StartsWith("//"))
        {
            symbols.Add(new { kind = "method", name = methodMatch.Groups[1].Value, signature = line.Trim(), line = lineNum });
        }
    }

    return symbols;
}

static string GetField(JsonElement fields, string key) =>
    fields.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null
        ? v.GetString() ?? string.Empty
        : string.Empty;

static string StripHtml(string html)
{
    if (string.IsNullOrWhiteSpace(html)) return string.Empty;
    var text = Regex.Replace(html, "<[^>]+>", " ");
    text = text.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
    return Regex.Replace(text, @"\s{2,}", " ").Trim();
}

static IReadOnlyList<string> ExtractKeywords(string query)
{
    var stopwords = new HashSet<string>
    {
        "a", "an", "the", "in", "on", "at", "to", "for", "of", "and", "or",
        "is", "are", "was", "were", "be", "been", "have", "has", "had",
        "do", "does", "did", "will", "would", "could", "should"
    };
    return query
        .ToLowerInvariant()
        .Split([' ', ',', '.', ':', ';', '!', '?', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
        .Where(w => w.Length > 2 && !stopwords.Contains(w))
        .Distinct()
        .ToList();
}

record RepoContextRequest(string Query);
record SaveSpecRequest(int WorkItemId, string Content);
record FindReferencesRequest(string Symbol);
