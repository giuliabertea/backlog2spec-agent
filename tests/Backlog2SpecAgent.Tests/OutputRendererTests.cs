using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Models;
using Backlog2SpecAgent.Cli.Output;

namespace Backlog2SpecAgent.Tests;

public class OutputRendererTests : IDisposable
{
    private readonly string _tempDir;

    public OutputRendererTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // --- Slugify ---

    [Fact]
    public void Slugify_SimpleTitle_ReturnsLowercaseHyphenated()
    {
        var slug = OutputRenderer.Slugify("Add Profitability Feature");
        Assert.Equal("add-profitability-feature", slug);
    }

    [Fact]
    public void Slugify_SpecialCharacters_AreReplacedWithHyphens()
    {
        var slug = OutputRenderer.Slugify("Fix: the bug (urgent!)");
        Assert.Equal("fix-the-bug-urgent", slug);
    }

    [Fact]
    public void Slugify_LeadingAndTrailingHyphens_AreTrimmed()
    {
        var slug = OutputRenderer.Slugify("  ---hello world---  ");
        Assert.Equal("hello-world", slug);
    }

    [Fact]
    public void Slugify_LongTitle_IsTruncatedAtMaxLength()
    {
        var title = string.Concat(Enumerable.Repeat("word ", 20)).Trim();
        var slug = OutputRenderer.Slugify(title, maxLength: 20);
        Assert.True(slug.Length <= 20);
    }

    [Fact]
    public void Slugify_EmptyTitle_ReturnsEmpty()
    {
        var slug = OutputRenderer.Slugify(string.Empty);
        Assert.Equal(string.Empty, slug);
    }

    [Fact]
    public void Slugify_NumbersPreserved()
    {
        var slug = OutputRenderer.Slugify("Feature 42 release");
        Assert.Contains("42", slug);
    }

    // --- WriteHierarchyToFiles ---

    [Fact]
    public void WriteHierarchyToFiles_CreatesSummaryFile()
    {
        var renderer = new OutputRenderer();
        var parent = new WorkItemDto { Id = 10, Title = "My Feature", WorkItemType = "Feature" };
        var children = new[] { (Item(11, "Child One"), Spec("Goal A")) };

        renderer.WriteHierarchyToFiles(parent, children, _tempDir);

        Assert.True(File.Exists(Path.Combine(_tempDir, "_summary.md")));
    }

    [Fact]
    public void WriteHierarchyToFiles_CreatesOneFilePerChild()
    {
        var renderer = new OutputRenderer();
        var parent = new WorkItemDto { Id = 10, Title = "My Feature", WorkItemType = "Feature" };
        var children = new[]
        {
            (Item(11, "Child One"), Spec("Goal A")),
            (Item(12, "Child Two"), Spec("Goal B"))
        };

        renderer.WriteHierarchyToFiles(parent, children, _tempDir);

        var files = Directory.GetFiles(_tempDir, "*.md")
            .Select(Path.GetFileName)
            .Where(f => f != "_summary.md")
            .ToList();

        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void WriteHierarchyToFiles_ChildFileNameContainsIdAndSlug()
    {
        var renderer = new OutputRenderer();
        var parent = new WorkItemDto { Id = 10, Title = "Feature", WorkItemType = "Feature" };
        var children = new[] { (Item(11, "Calculate Revenue"), Spec("Goal")) };

        renderer.WriteHierarchyToFiles(parent, children, _tempDir);

        Assert.True(File.Exists(Path.Combine(_tempDir, "11-calculate-revenue.md")));
    }

    [Fact]
    public void WriteHierarchyToFiles_SummaryContainsParentTitle()
    {
        var renderer = new OutputRenderer();
        var parent = new WorkItemDto { Id = 10, Title = "My Feature", WorkItemType = "Feature", Description = "Desc" };
        var children = new[] { (Item(11, "Child"), Spec("Goal")) };

        renderer.WriteHierarchyToFiles(parent, children, _tempDir);

        var summary = File.ReadAllText(Path.Combine(_tempDir, "_summary.md"));
        Assert.Contains("My Feature", summary);
    }

    [Fact]
    public void WriteHierarchyToFiles_ChildFileContainsGoal()
    {
        var renderer = new OutputRenderer();
        var parent = new WorkItemDto { Id = 10, Title = "Feature", WorkItemType = "Feature" };
        var spec = Spec("Improve booking accuracy");
        var children = new[] { (Item(11, "Child"), spec) };

        renderer.WriteHierarchyToFiles(parent, children, _tempDir);

        var content = File.ReadAllText(Path.Combine(_tempDir, "11-child.md"));
        Assert.Contains("Improve booking accuracy", content);
    }

    // --- RenderMarkdown ---

    [Fact]
    public void RenderMarkdown_CreatesFileAtGivenPath()
    {
        var renderer = new OutputRenderer();
        var path = Path.Combine(_tempDir, "spec.md");
        renderer.RenderMarkdown(Spec("My goal"), "My Title", 42, path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void RenderMarkdown_FileContainsGoalAndTitle()
    {
        var renderer = new OutputRenderer();
        var path = Path.Combine(_tempDir, "spec.md");
        renderer.RenderMarkdown(Spec("Revenue goal"), "Revenue Feature", 5, path);
        var content = File.ReadAllText(path);
        Assert.Contains("Revenue goal", content);
        Assert.Contains("Revenue Feature", content);
    }

    [Fact]
    public void RenderMarkdown_AppendsExtensionIfMissing()
    {
        var renderer = new OutputRenderer();
        var pathWithoutExt = Path.Combine(_tempDir, "spec-no-ext");
        renderer.RenderMarkdown(Spec("Goal"), "Title", 1, pathWithoutExt);
        Assert.True(File.Exists(pathWithoutExt + ".md"));
    }

    private static WorkItemDto Item(int id, string title) =>
        new() { Id = id, Title = title, WorkItemType = "Product Backlog Item" };

    private static GeneratedSpec Spec(string goal) => new()
    {
        Goal = goal,
        Behaviour = ["B1"],
        EdgeCases = ["E1"],
        OutOfScope = "Auth",
        FilesToChange = ["src/Foo.cs: add method"]
    };
}
