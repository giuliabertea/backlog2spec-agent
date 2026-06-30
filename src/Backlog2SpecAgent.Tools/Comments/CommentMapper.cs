using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backlog2SpecAgent.Tools.Comments;

public sealed record WorkItemComment(string Author, string Date, string Text);

public static class CommentMapper
{
    // ADO returns comments in an unspecified order (observed: newest first); sort
    // explicitly so the agent reads them as a timeline rather than a jumble.
    public static List<WorkItemComment> MapComments(JsonElement commentsArray)
    {
        var comments = new List<(DateTimeOffset CreatedDate, WorkItemComment Comment)>();

        foreach (var item in commentsArray.EnumerateArray())
        {
            var author = item.TryGetProperty("createdBy", out var createdBy) &&
                         createdBy.TryGetProperty("displayName", out var displayName)
                ? displayName.GetString() ?? string.Empty
                : string.Empty;

            var dateText = item.TryGetProperty("createdDate", out var createdDateProp)
                ? createdDateProp.GetString() ?? string.Empty
                : string.Empty;

            var rawText = item.TryGetProperty("text", out var textProp)
                ? textProp.GetString() ?? string.Empty
                : string.Empty;

            var createdDate = DateTimeOffset.TryParse(dateText, out var parsed) ? parsed : DateTimeOffset.MinValue;

            comments.Add((createdDate, new WorkItemComment(author, dateText, StripHtml(rawText))));
        }

        return comments
            .OrderBy(c => c.CreatedDate)
            .Select(c => c.Comment)
            .ToList();
    }

    public static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = text.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        return Regex.Replace(text, @"\s{2,}", " ").Trim();
    }
}
