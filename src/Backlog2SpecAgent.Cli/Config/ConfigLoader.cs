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

    // startDirectory is used in tests; production code relies on the default (cwd).
    private readonly string _startDirectory;

    public ConfigLoader(string? startDirectory = null)
    {
        _startDirectory = startDirectory ?? Directory.GetCurrentDirectory();
    }

    public async Task<BacklogConfig> LoadAsync(CancellationToken ct = default)
    {
        var configPath = FindConfigFile();
        if (configPath is null)
            return new BacklogConfig();

        BacklogConfig config;
        try
        {
            await using var stream = File.OpenRead(configPath);
            config = await JsonSerializer.DeserializeAsync<BacklogConfig>(stream, JsonOptions, ct)
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

    private static async Task LoadDevRulesAsync(BacklogConfig config, string configPath, CancellationToken ct)
    {
        if (config.DevRulesFiles.Count == 0)
            return;

        var configDir = Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();
        var parts = new List<string>();

        foreach (var file in config.DevRulesFiles)
        {
            if (string.IsNullOrWhiteSpace(file))
                continue;

            var rulesPath = Path.GetFullPath(file, configDir);

            if (!File.Exists(rulesPath))
                throw new ConfigException($"devRulesFiles entry not found: '{rulesPath}'");

            parts.Add(await File.ReadAllTextAsync(rulesPath, ct));
        }

        if (parts.Count > 0)
            config.DevRulesContent = string.Join("\n\n", parts);
    }

    private string? FindConfigFile()
    {
        var dir = new DirectoryInfo(_startDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ConfigFileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    private static void ValidateRequiredFields(BacklogConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Ado.Organization))
            throw new ConfigException("Missing required field: ado.organization");
        if (string.IsNullOrWhiteSpace(config.Ado.Project))
            throw new ConfigException("Missing required field: ado.project");
        if (string.IsNullOrWhiteSpace(config.Project.Name))
            throw new ConfigException("Missing required field: project.name");
    }
}
