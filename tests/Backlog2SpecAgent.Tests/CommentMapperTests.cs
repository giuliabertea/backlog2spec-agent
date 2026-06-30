using System.Text.Json;
using Backlog2SpecAgent.Tools.Comments;

namespace Backlog2SpecAgent.Tests;

public class CommentMapperTests
{
    private static JsonElement ParseCommentsArray(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void MapComments_HappyPath_MapsAuthorDateAndText()
    {
        var json = """
            [
              {
                "text": "Use the existing RetryPolicy class.",
                "createdBy": { "displayName": "Jane Doe" },
                "createdDate": "2024-01-02T10:00:00Z"
              }
            ]
            """;

        var result = CommentMapper.MapComments(ParseCommentsArray(json));

        var comment = Assert.Single(result);
        Assert.Equal("Jane Doe", comment.Author);
        Assert.Equal("2024-01-02T10:00:00Z", comment.Date);
        Assert.Equal("Use the existing RetryPolicy class.", comment.Text);
    }

    [Fact]
    public void MapComments_EmptyArray_ReturnsEmptyList()
    {
        var result = CommentMapper.MapComments(ParseCommentsArray("[]"));

        Assert.Empty(result);
    }

    [Fact]
    public void MapComments_OrdersChronologically_OldestFirst()
    {
        var json = """
            [
              { "text": "Second note", "createdBy": { "displayName": "A" }, "createdDate": "2024-03-01T00:00:00Z" },
              { "text": "First note", "createdBy": { "displayName": "B" }, "createdDate": "2024-01-01T00:00:00Z" },
              { "text": "Third note", "createdBy": { "displayName": "C" }, "createdDate": "2024-06-01T00:00:00Z" }
            ]
            """;

        var result = CommentMapper.MapComments(ParseCommentsArray(json));

        Assert.Equal(["First note", "Second note", "Third note"], result.Select(c => c.Text));
    }

    [Fact]
    public void MapComments_StripsHtmlTagsAndEntities()
    {
        var json = """
            [
              {
                "text": "<div>Check <b>UserRepository.cs</b>&nbsp;&amp; its tests.</div>",
                "createdBy": { "displayName": "Jane Doe" },
                "createdDate": "2024-01-02T10:00:00Z"
              }
            ]
            """;

        var result = CommentMapper.MapComments(ParseCommentsArray(json));

        Assert.Equal("Check UserRepository.cs & its tests.", result[0].Text);
    }

    [Fact]
    public void MapComments_MissingFields_DefaultToEmptyString()
    {
        var json = """
            [
              { "text": "No author or date" }
            ]
            """;

        var result = CommentMapper.MapComments(ParseCommentsArray(json));

        var comment = Assert.Single(result);
        Assert.Equal(string.Empty, comment.Author);
        Assert.Equal(string.Empty, comment.Date);
        Assert.Equal("No author or date", comment.Text);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("<p>Plain text</p>", "Plain text")]
    [InlineData("A &lt;tag&gt; example", "A <tag> example")]
    public void StripHtml_HandlesEdgeCases(string input, string expected)
    {
        Assert.Equal(expected, CommentMapper.StripHtml(input));
    }
}
