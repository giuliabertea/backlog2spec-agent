using Microsoft.Extensions.Configuration;

namespace Backlog2SpecAgent.Cli.Config;

// Binds BacklogConfig from .NET configuration (user-secrets locally, environment
// variables / App Service application settings in CI or hosted scenarios).
// This replaces the old backlog-2-spec.json file loader. There is now a single
// configuration source, independent of the current working directory.
// Environment variables use "__" where the key uses ":" e.g. ToolsApi__BaseUrl.
public sealed class ConfigLoader
{
    private readonly IConfiguration _configuration;
    private BacklogConfig? _cached;

    public ConfigLoader(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<BacklogConfig> LoadAsync(CancellationToken ct = default)
        => Task.FromResult(Load());

    public BacklogConfig Load()
    {
        if (_cached is not null)
            return _cached;

        var config = new BacklogConfig
        {
            Project       = _configuration.GetSection("Project").Get<ProjectConfig>() ?? new ProjectConfig(),
            Conventions   = _configuration.GetSection("Conventions").Get<ConventionsConfig>() ?? new ConventionsConfig(),
            Ado           = _configuration.GetSection("Ado").Get<AdoConfig>() ?? new AdoConfig(),
            ToolsApi      = _configuration.GetSection("ToolsApi").Get<ToolsApiConfig>() ?? new ToolsApiConfig(),
            DevRulesFiles = _configuration.GetSection("DevRules:Files").Get<string[]>() ?? []
        };

        ValidateRequiredFields(config);
        ResolveDevRules(config);

        _cached = config;
        return config;
    }

    // Dev rules can be supplied two ways:
    // 1. Inline via DevRules:Content (preferred).
    // 2. As file paths via DevRules:Files resolved relative to the current directory.
    private void ResolveDevRules(BacklogConfig config)
    {
        var inline = _configuration["DevRules:Content"];
        if (!string.IsNullOrWhiteSpace(inline))
        {
            config.DevRulesContent = inline;
            return;
        }

        if (config.DevRulesFiles.Count == 0)
            return;

        var baseDir = Directory.GetCurrentDirectory();
        var parts = new List<string>();

        foreach (var file in config.DevRulesFiles)
        {
            if (string.IsNullOrWhiteSpace(file))
                continue;

            var rulesPath = Path.GetFullPath(file, baseDir);
            if (!File.Exists(rulesPath))
                throw new ConfigException($"DevRules:Files entry not found: '{rulesPath}'");

            parts.Add(File.ReadAllText(rulesPath));
        }

        if (parts.Count > 0)
            config.DevRulesContent = string.Join("\n\n", parts);
    }

    private static void ValidateRequiredFields(BacklogConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Ado.Organization))
            throw new ConfigException("Missing required configuration: Ado:Organization");
        if (string.IsNullOrWhiteSpace(config.Ado.Project))
            throw new ConfigException("Missing required configuration: Ado:Project");
        if (string.IsNullOrWhiteSpace(config.Project.Name))
            throw new ConfigException("Missing required configuration: Project:Name");
    }
}
