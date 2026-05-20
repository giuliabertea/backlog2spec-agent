using System.CommandLine;
using System.CommandLine.Invocation;
using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Agents;
using Backlog2SpecAgent.Cli.Config;
using Backlog2SpecAgent.Cli.Models;
using Backlog2SpecAgent.Cli.Output;
using Microsoft.Extensions.Logging;

namespace Backlog2SpecAgent.Cli.Commands;

public sealed class SpecCommand : Command
{
    private readonly IAdoClient _adoClient;
    private readonly ISpecGeneratorAgent _specGeneratorAgent;
    private readonly IOutputRenderer _renderer;
    private readonly ILogger<SpecCommand> _logger;

    public SpecCommand(
        IAdoClient adoClient,
        ISpecGeneratorAgent specGeneratorAgent,
        IOutputRenderer renderer,
        ILogger<SpecCommand> logger)
        : base("spec", "Generate a structured spec from an Azure DevOps work item")
    {
        _adoClient = adoClient;
        _specGeneratorAgent = specGeneratorAgent;
        _renderer = renderer;
        _logger = logger;

        var idArg = new Argument<int>("id", "Azure DevOps work item ID");
        var rawOption = new Option<bool>("--raw", "Output JSON only, no formatting");
        var mockOption = new Option<bool>("--mock", "Run pipeline with mock implementations (no external calls)");
        var outputOption = new Option<string?>("--output", "Save spec to a markdown file at the given path");
        var featureOption = new Option<bool>("--feature", "Treat the work item as a Feature and export all child specs to a folder");
        var epicOption = new Option<bool>("--epic", "Treat the work item as an Epic and export all child specs to a folder");

        AddArgument(idArg);
        AddOption(rawOption);
        AddOption(mockOption);
        AddOption(outputOption);
        AddOption(featureOption);
        AddOption(epicOption);

        AddValidator(result =>
        {
            var isFeature = result.GetValueForOption(featureOption);
            var isEpic = result.GetValueForOption(epicOption);
            if (isFeature && isEpic)
                result.ErrorMessage = "--feature and --epic are mutually exclusive.";
        });

        this.SetHandler(async (InvocationContext context) =>
        {
            var id = context.ParseResult.GetValueForArgument(idArg);
            var raw = context.ParseResult.GetValueForOption(rawOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var isFeature = context.ParseResult.GetValueForOption(featureOption);
            var isEpic = context.ParseResult.GetValueForOption(epicOption);

            int exitCode;
            if (isFeature || isEpic)
                exitCode = await ExecuteHierarchyAsync(id, isFeature, isEpic, raw, CancellationToken.None);
            else
                exitCode = await ExecuteAsync(id, raw, output, CancellationToken.None);

            Environment.Exit(exitCode);
        });
    }

    private async Task<int> ExecuteAsync(int id, bool raw, string? output, CancellationToken ct)
    {
        try
        {
            if (!raw) _renderer.RenderProgress("Generating spec...");
            _logger.LogInformation("Generating spec for work item {WorkItemId}", id);
            var spec = await _specGeneratorAgent.GenerateAsync(id, ct);

            if (raw)
            {
                _renderer.RenderRaw(spec);
            }
            else
            {
                _renderer.RenderSpec(spec, false);
                if (!string.IsNullOrEmpty(output))
                    _renderer.RenderMarkdown(spec, $"Work Item #{id}", id, output);
            }

            return 0;
        }
        catch (ConfigException ex)
        {
            _logger.LogError(ex, "Configuration error");
            _renderer.RenderError($"Configuration error: {ex.Message}");
            return 1;
        }
        catch (AdoNotFoundException ex)
        {
            _logger.LogError(ex, "Work item not found");
            _renderer.RenderError(ex.Message);
            return 1;
        }
        catch (AdoAuthException ex)
        {
            _logger.LogError(ex, "ADO authentication error");
            _renderer.RenderError($"Authentication error: {ex.Message}");
            return 1;
        }
        catch (LlmFormatException ex)
        {
            _logger.LogError(ex, "LLM returned invalid JSON");
            _renderer.RenderError($"AI response error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error");
            _renderer.RenderError($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> ExecuteHierarchyAsync(
        int id, bool isFeature, bool isEpic, bool raw, CancellationToken ct)
    {
        var expectedType = isFeature ? "Feature" : "Epic";

        try
        {
            if (!raw) _renderer.RenderProgress($"Fetching {expectedType} #{id} with children...");
            _logger.LogInformation("Fetching hierarchy for work item {WorkItemId}", id);
            var hierarchy = await _adoClient.GetWorkItemHierarchyAsync(id, ct);

            if (!string.Equals(hierarchy.Parent.WorkItemType, expectedType, StringComparison.OrdinalIgnoreCase))
            {
                _renderer.RenderError(
                    $"Work item {id} is of type '{hierarchy.Parent.WorkItemType}', not '{expectedType}'. " +
                    $"Use --{hierarchy.Parent.WorkItemType.ToLowerInvariant()} or remove the flag.");
                return 1;
            }

            if (hierarchy.Children.Count == 0)
            {
                _renderer.RenderError($"{expectedType} #{id} has no child work items.");
                return 1;
            }

            var results = new List<(WorkItemDto Item, GeneratedSpec Spec)>();
            var skipped = new List<int>();
            var total = hierarchy.Children.Count;

            for (var i = 0; i < total; i++)
            {
                var child = hierarchy.Children[i];
                if (!raw) _renderer.RenderProgress($"Generating spec for #{child.Id} ({i + 1}/{total})...");
                _logger.LogInformation("Processing child work item {WorkItemId}", child.Id);

                try
                {
                    var spec = await _specGeneratorAgent.GenerateAsync(child.Id, ct);
                    results.Add((child, spec));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate spec for child {WorkItemId}, skipping", child.Id);
                    _renderer.RenderProgress($"⚠ Skipped #{child.Id}: {ex.Message}");
                    skipped.Add(child.Id);
                }
            }

            if (results.Count == 0)
            {
                _renderer.RenderError($"All {total} child work items failed to generate. No files written.");
                return 1;
            }

            if (raw)
            {
                foreach (var (_, spec) in results)
                    _renderer.RenderRaw(spec);
            }
            else
            {
                var slug = OutputRenderer.Slugify(hierarchy.Parent.Title);
                var outputDir = Path.Combine("spec", $"{id}-{slug}");
                _renderer.WriteHierarchyToFiles(hierarchy.Parent, results, outputDir);
                _renderer.RenderProgress(
                    $"Done. {results.Count}/{total} specs written to {outputDir}" +
                    (skipped.Count > 0 ? $" ({skipped.Count} skipped: #{string.Join(", #", skipped)})" : string.Empty));
            }

            return 0;
        }
        catch (ConfigException ex)
        {
            _logger.LogError(ex, "Configuration error");
            _renderer.RenderError($"Configuration error: {ex.Message}");
            return 1;
        }
        catch (AdoNotFoundException ex)
        {
            _logger.LogError(ex, "Work item not found");
            _renderer.RenderError(ex.Message);
            return 1;
        }
        catch (AdoAuthException ex)
        {
            _logger.LogError(ex, "ADO authentication error");
            _renderer.RenderError($"Authentication error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error");
            _renderer.RenderError($"Unexpected error: {ex.Message}");
            return 1;
        }
    }
}
