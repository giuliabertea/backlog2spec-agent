namespace Backlog2SpecAgent.Cli.Config;

public sealed class BacklogConfig
{
    public ProjectConfig Project { get; init; } = new();
    public ConventionsConfig Conventions { get; init; } = new();
    public AdoConfig Ado { get; init; } = new();
    public ToolsApiConfig ToolsApi { get; init; } = new();
    public IReadOnlyList<string> DevRulesFiles { get; init; } = [];
    public string? DevRulesContent { get; internal set; }
}

public sealed class ProjectConfig
{
    public string Name { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public string Framework { get; init; } = string.Empty;
    public string TestFramework { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class ConventionsConfig
{
    public string? Naming { get; init; }
    public string? FolderStructure { get; init; }
    public string? SpecStyle { get; init; }
    public string? DiPattern { get; init; }
    public string? ErrorHandling { get; init; }
    public string? Testing { get; init; }
}

public sealed class AdoConfig
{
    public string Organization { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string? RepoName { get; init; }
    public string? Branch { get; init; }
}

public sealed class ToolsApiConfig
{
    public string BaseUrl { get; init; } = string.Empty;
}
