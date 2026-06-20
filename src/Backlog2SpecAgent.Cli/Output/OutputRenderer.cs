using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Models;
using Spectre.Console;


namespace Backlog2SpecAgent.Cli.Output;

public sealed class OutputRenderer : IOutputRenderer
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void RenderProgress(string step)
    {
        AnsiConsole.MarkupLine($"[grey]→[/] [dim]{Markup.Escape(step)}[/]");
    }

    public void RenderSpec(GeneratedSpec spec, bool verbose)
    {
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold blue]── Goal ─────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine($"[white]{Markup.Escape(spec.Goal)}[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold blue]── Behaviour ────────────────────────────────────[/]");
        foreach (var b in spec.Behaviour)
            AnsiConsole.MarkupLine($"[white]  • {Markup.Escape(b)}[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold blue]── Edge Cases ───────────────────────────────────[/]");
        foreach (var ec in spec.EdgeCases)
            AnsiConsole.MarkupLine($"[yellow]  ⚠ {Markup.Escape(ec)}[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold blue]── Out of Scope ─────────────────────────────────[/]");
        AnsiConsole.MarkupLine($"[white]{Markup.Escape(spec.OutOfScope)}[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold blue]── Files to Change ──────────────────────────────[/]");
        foreach (var f in spec.FilesToChange)
        {
            var badge = f.Confidence?.ToLowerInvariant() switch
            {
                "high"   => " [green]●[/]",
                "medium" => " [yellow]●[/]",
                "low"    => " [red]●[/]",
                _        => string.Empty
            };
            AnsiConsole.MarkupLine($"[white]  • [bold]{Markup.Escape(f.File)}[/]: {Markup.Escape(f.Change)}[/]{badge}");
            if (!string.IsNullOrWhiteSpace(f.Evidence))
                AnsiConsole.MarkupLine($"[dim]      {Markup.Escape(f.Evidence)}[/]");
        }
        AnsiConsole.WriteLine();

        if (spec.OpenQuestions.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold blue]── Open Questions ───────────────────────────────[/]");
            foreach (var q in spec.OpenQuestions)
                AnsiConsole.MarkupLine($"[yellow]  ? {Markup.Escape(q)}[/]");
            AnsiConsole.WriteLine();
        }

        if (spec.Conventions.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold blue]── Conventions ──────────────────────────────────[/]");
            foreach (var c in spec.Conventions)
                AnsiConsole.MarkupLine($"[white]  • {Markup.Escape(c)}[/]");
            AnsiConsole.WriteLine();
        }
    }

    public void RenderVerboseDetail(EnrichedTicket enriched)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold blue]── Enrichment Detail ────────────────────────────[/]");

        if (enriched.Ambiguities.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Ambiguities:[/]");
            foreach (var a in enriched.Ambiguities)
                AnsiConsole.MarkupLine($"[red]  ? {Markup.Escape(a)}[/]");
        }

        if (enriched.MissingAcceptanceCriteria.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Missing Acceptance Criteria:[/]");
            foreach (var m in enriched.MissingAcceptanceCriteria)
                AnsiConsole.MarkupLine($"[white]  • {Markup.Escape(m)}[/]");
        }

        if (enriched.Constraints.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Constraints:[/]");
            foreach (var c in enriched.Constraints)
                AnsiConsole.MarkupLine($"[white]  • {Markup.Escape(c)}[/]");
        }

        AnsiConsole.WriteLine();
    }

    public void RenderError(string message)
    {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] [red]{Markup.Escape(message)}[/]");
    }

    public void RenderRaw(GeneratedSpec spec)
    {
        var json = JsonSerializer.Serialize(spec, PrettyJson);
        Console.WriteLine(json);
    }

    public void RenderMarkdown(GeneratedSpec spec, string title, int workItemId, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Spec: {title}");
        sb.AppendLine();
        sb.AppendLine($"> Work Item: #{workItemId}  ");
        sb.AppendLine($"> Generated: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Goal");
        sb.AppendLine();
        sb.AppendLine(spec.Goal);
        sb.AppendLine();
        sb.AppendLine("## Behaviour");
        sb.AppendLine();
        foreach (var b in spec.Behaviour)
            sb.AppendLine($"- {b}");
        sb.AppendLine();
        sb.AppendLine("## Edge Cases");
        sb.AppendLine();
        foreach (var ec in spec.EdgeCases)
            sb.AppendLine($"- {ec}");
        sb.AppendLine();
        sb.AppendLine("## Out of Scope");
        sb.AppendLine();
        sb.AppendLine(spec.OutOfScope);
        sb.AppendLine();
        sb.AppendLine("## Files to Change");
        sb.AppendLine();
        foreach (var f in spec.FilesToChange)
        {
            sb.AppendLine($"- **{f.File}**: {f.Change}");
            if (!string.IsNullOrWhiteSpace(f.Confidence))
                sb.AppendLine($"  - Confidence: {f.Confidence}");
            if (!string.IsNullOrWhiteSpace(f.Evidence))
                sb.AppendLine($"  - Evidence: {f.Evidence}");
        }

        if (spec.OpenQuestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Open Questions");
            sb.AppendLine();
            foreach (var q in spec.OpenQuestions)
                sb.AppendLine($"- {q}");
        }

        if (spec.Conventions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Conventions");
            sb.AppendLine();
            foreach (var c in spec.Conventions)
                sb.AppendLine($"- {c}");
        }

        var path = outputPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? outputPath
            : outputPath + ".md";

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        AnsiConsole.MarkupLine($"[grey]→[/] [dim]Spec saved to {Markup.Escape(path)}[/]");
    }

    public void WriteHierarchyToFiles(WorkItemDto parent, IEnumerable<(WorkItemDto Item, GeneratedSpec Spec)> children, string outputDir)
    {
        var noBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        Directory.CreateDirectory(outputDir);

        var childList = children.ToList();

        var summary = new StringBuilder();
        summary.AppendLine($"# {parent.WorkItemType}: {parent.Title}");
        summary.AppendLine();
        summary.AppendLine($"> Work Item: #{parent.Id}  ");
        summary.AppendLine($"> Type: {parent.WorkItemType}  ");
        summary.AppendLine($"> Generated: {DateTime.UtcNow:yyyy-MM-dd}");
        summary.AppendLine();

        if (!string.IsNullOrWhiteSpace(parent.Description))
        {
            summary.AppendLine("## Description");
            summary.AppendLine();
            summary.AppendLine(parent.Description);
            summary.AppendLine();
        }

        summary.AppendLine("## Child Work Items");
        summary.AppendLine();
        summary.AppendLine("| ID | Title | Spec File |");
        summary.AppendLine("|----|-------|-----------|");
        foreach (var (item, _) in childList)
        {
            var fileName = $"{item.Id}-{Slugify(item.Title)}.md";
            summary.AppendLine($"| #{item.Id} | {item.Title} | [{fileName}]({fileName}) |");
        }

        File.WriteAllText(Path.Combine(outputDir, "_summary.md"), summary.ToString(), noBom);
        AnsiConsole.MarkupLine($"[grey]→[/] [dim]Summary saved to {Markup.Escape(Path.Combine(outputDir, "_summary.md"))}[/]");

        foreach (var (item, spec) in childList)
        {
            var fileName = $"{item.Id}-{Slugify(item.Title)}.md";
            var filePath = Path.Combine(outputDir, fileName);

            var sb = new StringBuilder();
            sb.AppendLine($"# Spec: {item.Title}");
            sb.AppendLine();
            sb.AppendLine($"> Work Item: #{item.Id}  ");
            sb.AppendLine($"> Generated: {DateTime.UtcNow:yyyy-MM-dd}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Goal");
            sb.AppendLine();
            sb.AppendLine(spec.Goal);
            sb.AppendLine();
            sb.AppendLine("## Behaviour");
            sb.AppendLine();
            foreach (var b in spec.Behaviour)
                sb.AppendLine($"- {b}");
            sb.AppendLine();
            sb.AppendLine("## Edge Cases");
            sb.AppendLine();
            foreach (var ec in spec.EdgeCases)
                sb.AppendLine($"- {ec}");
            sb.AppendLine();
            sb.AppendLine("## Out of Scope");
            sb.AppendLine();
            sb.AppendLine(spec.OutOfScope);
            sb.AppendLine();
            sb.AppendLine("## Files to Change");
            sb.AppendLine();
            foreach (var f in spec.FilesToChange)
            {
                sb.AppendLine($"- **{f.File}**: {f.Change}");
                if (!string.IsNullOrWhiteSpace(f.Confidence))
                    sb.AppendLine($"  - Confidence: {f.Confidence}");
                if (!string.IsNullOrWhiteSpace(f.Evidence))
                    sb.AppendLine($"  - Evidence: {f.Evidence}");
            }

            if (spec.OpenQuestions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Open Questions");
                sb.AppendLine();
                foreach (var q in spec.OpenQuestions)
                    sb.AppendLine($"- {q}");
            }

            if (spec.Conventions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Conventions");
                sb.AppendLine();
                foreach (var c in spec.Conventions)
                    sb.AppendLine($"- {c}");
            }

            File.WriteAllText(filePath, sb.ToString(), noBom);
            AnsiConsole.MarkupLine($"[grey]→[/] [dim]Spec saved to {Markup.Escape(filePath)}[/]");
        }
    }

    internal static string Slugify(string title, int maxLength = 60)
    {
        var slug = title.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9]+", "-");
        slug = slug.Trim('-');
        if (slug.Length > maxLength)
            slug = slug[..maxLength].TrimEnd('-');
        return slug;
    }
}
