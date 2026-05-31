using Backlog2SpecAgent.Cli.Config;
using HtmlAgilityPack;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Backlog2SpecAgent.Cli.Ado;

public sealed class AdoClient : IAdoClient
{
    private readonly ConfigLoader _configLoader;
    private readonly string _pat;

    public AdoClient(ConfigLoader configLoader, string pat)
    {
        _configLoader = configLoader;
        _pat = pat;
    }

    public async Task<WorkItemDto> GetWorkItemAsync(int id, CancellationToken ct = default)
    {
        var config = await _configLoader.LoadAsync(ct);

        WorkItemTrackingHttpClient client;
        try
        {
            var credentials = new VssBasicCredential(string.Empty, _pat);
            var connection = new VssConnection(new Uri(config.Ado.Organization), credentials);
            client = connection.GetClient<WorkItemTrackingHttpClient>();
        }
        catch (Exception ex)
        {
            throw new AdoAuthException("Failed to connect to Azure DevOps. Check your PAT and organization URL.", ex);
        }

        WorkItem workItem;
        try
        {
            workItem = await client.GetWorkItemAsync(config.Ado.Project, id, expand: WorkItemExpand.All, cancellationToken: ct);
        }
        catch (Exception ex) when (IsNotFoundError(ex)) { throw new AdoNotFoundException(id); }
        catch (Exception ex) when (IsAuthError(ex)) { throw new AdoAuthException("Authentication failed. Verify your PAT has the required permissions.", ex); }

        return MapToDto(id, workItem);
    }

    public async Task<WorkItemHierarchyDto> GetWorkItemHierarchyAsync(int parentId, CancellationToken ct = default)
    {
        var config = await _configLoader.LoadAsync(ct);

        WorkItemTrackingHttpClient client;
        try
        {
            var credentials = new VssBasicCredential(string.Empty, _pat);
            var connection = new VssConnection(new Uri(config.Ado.Organization), credentials);
            client = connection.GetClient<WorkItemTrackingHttpClient>();
        }
        catch (Exception ex)
        {
            throw new AdoAuthException("Failed to connect to Azure DevOps. Check your PAT and organization URL.", ex);
        }

        WorkItem parent;
        try
        {
            parent = await client.GetWorkItemAsync(config.Ado.Project, parentId, expand: WorkItemExpand.Relations, cancellationToken: ct);
        }
        catch (Exception ex) when (IsNotFoundError(ex)) { throw new AdoNotFoundException(parentId); }
        catch (Exception ex) when (IsAuthError(ex)) { throw new AdoAuthException("Authentication failed. Verify your PAT has the required permissions.", ex); }

        var childIds = ExtractChildIds(parent);
        IReadOnlyList<WorkItemDto> children = [];

        if (childIds.Count > 0)
        {
            var childWorkItems = await client.GetWorkItemsAsync(childIds, expand: WorkItemExpand.All, cancellationToken: ct);
            children = childWorkItems
                .Select(wi => MapToDto((int)wi.Id!, wi))
                .ToList();
        }

        return new WorkItemHierarchyDto(MapToDto(parentId, parent), children);
    }

    private static List<int> ExtractChildIds(WorkItem workItem)
    {
        if (workItem.Relations is null) return [];

        var ids = new List<int>();
        foreach (var rel in workItem.Relations)
        {
            if (rel.Rel != "System.LinkTypes.Hierarchy-Forward") continue;
            var url = rel.Url;
            if (string.IsNullOrEmpty(url)) continue;
            var lastSegment = url.Split('/').LastOrDefault();
            if (int.TryParse(lastSegment, out var childId))
                ids.Add(childId);
        }
        return ids;
    }

    private static WorkItemDto MapToDto(int id, WorkItem workItem) => new()
    {
        Id = id,
        Title = GetField(workItem, "System.Title"),
        WorkItemType = GetField(workItem, "System.WorkItemType"),
        Description = StripHtml(GetField(workItem, "System.Description")),
        AcceptanceCriteria = StripHtml(GetField(workItem, "Microsoft.VSTS.Common.AcceptanceCriteria"))
    };

    private static string GetField(WorkItem workItem, string fieldName)
    {
        if (workItem.Fields is null) return string.Empty;
        return workItem.Fields.TryGetValue(fieldName, out var value) && value is not null
            ? value.ToString() ?? string.Empty
            : string.Empty;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode.InnerText.Trim();
    }

    private static bool IsNotFoundError(Exception ex) =>
        ex.Message.Contains("does not exist") || ex.Message.Contains("not found") ||
        ex.Message.Contains("TF401232") || ex.Message.Contains("VS403417");

    private static bool IsAuthError(Exception ex) =>
        ex.Message.Contains("unauthorized") || ex.Message.Contains("Unauthorized") ||
        ex.Message.Contains("Access Denied");
}
