using System.Text.Json;

namespace Backlog2SpecAgent.Cli.Config;

public sealed class ConfigLoader
{
    private const string ConfigFileName = "backlog-2-spec.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<AgentConfig> LoadAsync(CancellationToken ct = default)
    {
        var configPath = FindConfigFile();
        AgentConfig config;
        try
        {
            await using var stream = File.OpenRead(configPath);
            config = await JsonSerializer.DeserializeAsync<AgentConfig>(stream, JsonOptions, ct)
                     ?? throw new ConfigException($"'{configPath}' is empty or null.");
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Failed to parse '{configPath}': {ex.Message}", ex);
        }

        ValidateRequiredFields(config);
        await LoadDevRulesAsync(config, configPath, ct);
        return config;
    }

    private static async Task LoadDevRulesAsync(AgentConfig config, string configPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.DevRulesFile))
            return;

        var configDir = Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();
        var rulesPath = Path.GetFullPath(config.DevRulesFile, configDir);

        if (!File.Exists(rulesPath))
            throw new ConfigException($"devRulesFile not found: '{rulesPath}'");

        config.DevRulesContent = await File.ReadAllTextAsync(rulesPath, ct);
    }

    private static string FindConfigFile()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ConfigFileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new ConfigException(
            $"'{ConfigFileName}' not found. Searched from '{Directory.GetCurrentDirectory()}' upward. " +
            $"Create a '{ConfigFileName}' file in your project or any ancestor directory.");
    }

    private static void ValidateRequiredFields(AgentConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Ado.Organization))
            throw new ConfigException("Missing required field: ado.organization");
        if (string.IsNullOrWhiteSpace(config.Ado.Project))
            throw new ConfigException("Missing required field: ado.project");
        if (string.IsNullOrWhiteSpace(config.Project.Name))
            throw new ConfigException("Missing required field: project.name");
    }
}
