namespace Backlog2SpecAgent.Cli.Config;

public sealed class AgentConfig
{
    public ProjectConfig Project { get; init; } = new();
    public ConventionsConfig Conventions { get; init; } = new();
    public AdoConfig Ado { get; init; } = new();
    public string? DevRulesFile { get; init; }
    public string? DevRulesContent { get; internal set; }
}

public sealed class ProjectConfig
{
    public string Name { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public string Framework { get; init; } = string.Empty;
    public string TestFramework { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
}

public sealed class ConventionsConfig
{
    public string Naming { get; init; } = string.Empty;
    public string FolderStructure { get; init; } = string.Empty;
    public string SpecStyle { get; init; } = string.Empty;
    public string DiPattern { get; init; } = string.Empty;
}

public sealed class AdoConfig
{
    public string Organization { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string? RepoName { get; init; }
    public string? Branch { get; init; }
}
