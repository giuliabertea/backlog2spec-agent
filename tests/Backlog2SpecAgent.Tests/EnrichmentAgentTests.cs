using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Agents;

namespace Backlog2SpecAgent.Tests;

// StopwordKeywordExtractor is the pure-logic component in the enrichment pipeline.
public class EnrichmentAgentTests
{
    private static WorkItemDto Item(string title, string type = "User Story") =>
        new() { Id = 1, Title = title, WorkItemType = type };

    [Fact]
    public async Task ExtractAsync_EmptyTitle_ReturnsEmptyList()
    {
        var extractor = new StopwordKeywordExtractor();
        var keywords = await extractor.ExtractAsync(Item(""));
        Assert.Empty(keywords);
    }

    [Fact]
    public async Task ExtractAsync_WhitespaceTitle_ReturnsEmptyList()
    {
        var extractor = new StopwordKeywordExtractor();
        var keywords = await extractor.ExtractAsync(Item("   "));
        Assert.Empty(keywords);
    }

    [Fact]
    public async Task ExtractAsync_TitleWithOnlyStopwords_ReturnsEmptyList()
    {
        var extractor = new StopwordKeywordExtractor();
        // All words are stopwords; "Bug" is ≤3 chars so also filtered
        var keywords = await extractor.ExtractAsync(Item("add new update fix with", "Bug"));
        Assert.Empty(keywords);
    }

    [Fact]
    public async Task ExtractAsync_TitleWithContent_FiltersStopwords()
    {
        var extractor = new StopwordKeywordExtractor();
        var keywords = await extractor.ExtractAsync(Item("Add the profitability calculation feature"));
        // "add", "the" are stopwords; "profitability", "calculation", "feature" pass
        Assert.DoesNotContain("add", keywords);
        Assert.DoesNotContain("the", keywords);
        Assert.Contains("profitability", keywords);
        Assert.Contains("calculation", keywords);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsAtMostFiveKeywords()
    {
        var extractor = new StopwordKeywordExtractor();
        var keywords = await extractor.ExtractAsync(
            Item("booking profitability calculation revenue margin delta analysis"));
        Assert.True(keywords.Count <= 5);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsDistinctKeywords()
    {
        var extractor = new StopwordKeywordExtractor();
        var keywords = await extractor.ExtractAsync(Item("booking booking revenue revenue"));
        Assert.Equal(keywords.Count, keywords.Distinct().Count());
    }

    [Fact]
    public async Task ExtractAsync_IncludesWorkItemTypeInText()
    {
        var extractor = new StopwordKeywordExtractor();
        // "Feature" has 7 chars and is not a stopword — should be included
        var keywords = await extractor.ExtractAsync(Item("booking summary", "Feature"));
        Assert.Contains("feature", keywords);
    }

    [Fact]
    public async Task ExtractAsync_ShortWordsUnderThreeChars_AreExcluded()
    {
        var extractor = new StopwordKeywordExtractor();
        // "is", "an", "do" are ≤3 chars
        var keywords = await extractor.ExtractAsync(Item("is an do booking"));
        Assert.DoesNotContain("is", keywords);
        Assert.DoesNotContain("an", keywords);
        Assert.DoesNotContain("do", keywords);
    }

    [Fact]
    public async Task ExtractAsync_KeywordsAreLowercase()
    {
        var extractor = new StopwordKeywordExtractor();
        var keywords = await extractor.ExtractAsync(Item("Booking Revenue Margin"));
        Assert.All(keywords, k => Assert.Equal(k, k.ToLowerInvariant()));
    }

    [Fact]
    public async Task ExtractAsync_SplitsOnDelimiters()
    {
        var extractor = new StopwordKeywordExtractor();
        // Slash and hyphen are split characters
        var keywords = await extractor.ExtractAsync(Item("booking/revenue-margin"));
        Assert.Contains("booking", keywords);
        Assert.Contains("revenue", keywords);
        Assert.Contains("margin", keywords);
    }
}
