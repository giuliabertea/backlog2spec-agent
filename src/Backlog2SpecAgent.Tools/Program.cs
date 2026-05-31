using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

var app = builder.Build();

// ── X-Api-Key middleware ────────────────────────────────────────────────────
var requiredApiKey = app.Configuration["Security:ApiKey"] ?? string.Empty;
app.Use(async (ctx, next) =>
{
    if (!string.IsNullOrEmpty(requiredApiKey))
    {
        if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var provided) ||
            !string.Equals(provided, requiredApiKey, StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsync("Unauthorized");
            return;
        }
    }
    await next(ctx);
});

// ── GET /workitem/{id} ──────────────────────────────────────────────────────
app.MapGet("/workitem/{id:int}", async (int id, IConfiguration config, IHttpClientFactory factory) =>
{
    var org = config["Ado:Organization"];
    var project = config["Ado:Project"];
    var pat = config["Ado:Pat"];

    if (string.IsNullOrEmpty(org) || string.IsNullOrEmpty(project) || string.IsNullOrEmpty(pat))
        return Results.Problem("ADO configuration incomplete. Set Ado:Organization, Ado:Project, and Ado:Pat.");

    using var http = factory.CreateClient();
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

    var url = $"{org.TrimEnd('/')}/{project}/_apis/wit/workitems/{id}?$expand=all&api-version=7.1";
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
        title            = GetField(fields, "System.Title"),
        workItemType     = GetField(fields, "System.WorkItemType"),
        description      = StripHtml(GetField(fields, "System.Description")),
        acceptanceCriteria = StripHtml(GetField(fields, "Microsoft.VSTS.Common.AcceptanceCriteria"))
    });
});

// ── GET /workitem/{id}/hierarchy ────────────────────────────────────────────
app.MapGet("/workitem/{id:int}/hierarchy", async (int id, IConfiguration config, IHttpClientFactory factory) =>
{
    var org = config["Ado:Organization"];
    var project = config["Ado:Project"];
    var pat = config["Ado:Pat"];

    if (string.IsNullOrEmpty(org) || string.IsNullOrEmpty(project) || string.IsNullOrEmpty(pat))
        return Results.Problem("ADO configuration incomplete. Set Ado:Organization, Ado:Project, and Ado:Pat.");

    using var http = factory.CreateClient();
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

    // 1. Fetch parent with relations expanded
    var parentUrl = $"{org.TrimEnd('/')}/{project}/_apis/wit/workitems/{id}?$expand=relations&api-version=7.1";
    var parentResp = await http.GetAsync(parentUrl);

    if (parentResp.StatusCode == System.Net.HttpStatusCode.NotFound)
        return Results.NotFound(new { error = $"Work item {id} not found." });

    if (!parentResp.IsSuccessStatusCode)
        return Results.Problem($"ADO returned {(int)parentResp.StatusCode}: {parentResp.ReasonPhrase}");

    var parentJson = await parentResp.Content.ReadAsStringAsync();
    using var parentDoc = JsonDocument.Parse(parentJson);
    var parentFields = parentDoc.RootElement.GetProperty("fields");

    var parentDto = new
    {
        id,
        title            = GetField(parentFields, "System.Title"),
        workItemType     = GetField(parentFields, "System.WorkItemType"),
        description      = StripHtml(GetField(parentFields, "System.Description")),
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
                        title            = GetField(childFields, "System.Title"),
                        workItemType     = GetField(childFields, "System.WorkItemType"),
                        description      = StripHtml(GetField(childFields, "System.Description")),
                        acceptanceCriteria = StripHtml(GetField(childFields, "Microsoft.VSTS.Common.AcceptanceCriteria"))
                    });
                }
            }
        }
    }

    return Results.Ok(new { parent = parentDto, children = childrenDtos });
});

// ── POST /repo-context ──────────────────────────────────────────────────────
app.MapPost("/repo-context", async (RepoContextRequest req, IConfiguration config, IHttpClientFactory factory) =>
{
    var org    = config["Ado:Organization"];
    var project = config["Ado:Project"];
    var repo   = config["Ado:RepoName"];
    var branch = config["Ado:Branch"] ?? "main";
    var pat    = config["Ado:Pat"];

    if (string.IsNullOrEmpty(repo))
        return Results.Ok(Array.Empty<object>());

    if (string.IsNullOrEmpty(org) || string.IsNullOrEmpty(project) || string.IsNullOrEmpty(pat))
        return Results.Problem("ADO configuration incomplete.");

    using var http = factory.CreateClient();
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

    // List all file paths in the repo
    var listUrl = $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repo}/items" +
                  $"?recursionLevel=full&versionDescriptor.version={Uri.EscapeDataString(branch)}" +
                  $"&versionDescriptor.versionType=branch&api-version=7.1";

    var listResp = await http.GetAsync(listUrl);
    if (!listResp.IsSuccessStatusCode) return Results.Ok(Array.Empty<object>());

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

        var encoded = Uri.EscapeDataString(filePath);
        var fileUrl = $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repo}/items" +
                      $"?path={encoded}&includeContent=true&versionDescriptor.version={Uri.EscapeDataString(branch)}" +
                      $"&versionDescriptor.versionType=branch&api-version=7.1";

        var fileResp = await http.GetAsync(fileUrl);
        if (!fileResp.IsSuccessStatusCode) continue;

        string content;
        using (var fileDoc = JsonDocument.Parse(await fileResp.Content.ReadAsStringAsync()))
            content = fileDoc.RootElement.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";

        if (content.Length > maxChars) content = content[..maxChars] + "...";
        results.Add(new { path = filePath, content });
    }

    return Results.Ok(results);
});

// ── POST /spec ──────────────────────────────────────────────────────────────
app.MapPost("/spec", async (SaveSpecRequest req, IConfiguration config) =>
{
    var dir = config["SpecOutput:Directory"] ?? "./specs";
    Directory.CreateDirectory(dir);
    var filePath = Path.Combine(dir, $"spec-{req.WorkItemId}.md");
    await File.WriteAllTextAsync(filePath, req.Content, Encoding.UTF8);
    return Results.Ok(new { saved = filePath });
});

app.Run();

// ── Helpers ─────────────────────────────────────────────────────────────────
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
