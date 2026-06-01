using System.Text;
using Backlog2SpecAgent.Cli.Config;

namespace Backlog2SpecAgent.Tests;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task LoadAsync_ValidConfigFile_ReturnsPopulatedAgentConfig()
    {
        WriteConfig(_tempDir, """
            {
              "project": { "name": "MyApp", "language": "C#", "framework": ".NET 8", "architecture": "Clean" },
              "conventions": { "naming": "PascalCase" },
              "ado": { "organization": "https://dev.azure.com/myorg", "project": "MyProject" }
            }
            """);

        var loader = new ConfigLoader(_tempDir);
        var config = await loader.LoadAsync();

        Assert.Equal("MyApp", config.Project.Name);
        Assert.Equal("C#", config.Project.Language);
        Assert.Equal("https://dev.azure.com/myorg", config.Ado.Organization);
        Assert.Equal("MyProject", config.Ado.Project);
    }

    [Fact]
    public async Task LoadAsync_NoConfigFile_ReturnsDefaultAgentConfig()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var loader = new ConfigLoader(emptyDir);
        var config = await loader.LoadAsync();

        Assert.NotNull(config);
        Assert.Empty(config.Project.Name);
        Assert.Empty(config.Ado.Organization);
    }

    [Fact]
    public async Task LoadAsync_MissingAdoOrganization_ThrowsConfigException()
    {
        WriteConfig(_tempDir, """
            {
              "project": { "name": "MyApp" },
              "ado": { "organization": "", "project": "MyProject" }
            }
            """);

        var loader = new ConfigLoader(_tempDir);
        await Assert.ThrowsAsync<ConfigException>(() => loader.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_MissingProjectName_ThrowsConfigException()
    {
        WriteConfig(_tempDir, """
            {
              "project": { "name": "" },
              "ado": { "organization": "https://dev.azure.com/myorg", "project": "MyProject" }
            }
            """);

        var loader = new ConfigLoader(_tempDir);
        await Assert.ThrowsAsync<ConfigException>(() => loader.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ThrowsConfigException()
    {
        WriteConfig(_tempDir, "{ this is not valid json }}}");

        var loader = new ConfigLoader(_tempDir);
        await Assert.ThrowsAsync<ConfigException>(() => loader.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_WithDevRulesFiles_LoadsAndConcatenatesContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "rules1.md"), "No AutoMapper.", Encoding.UTF8);
        File.WriteAllText(Path.Combine(_tempDir, "rules2.md"), "Use Result<T>.", Encoding.UTF8);

        WriteConfig(_tempDir, $$"""
            {
              "project": { "name": "MyApp" },
              "ado": { "organization": "https://dev.azure.com/myorg", "project": "MyProject" },
              "devRulesFiles": ["rules1.md", "rules2.md"]
            }
            """);

        var loader = new ConfigLoader(_tempDir);
        var config = await loader.LoadAsync();

        Assert.Equal("No AutoMapper.\n\nUse Result<T>.", config.DevRulesContent);
    }

    [Fact]
    public async Task LoadAsync_DevRulesFilesMissing_ThrowsConfigException()
    {
        WriteConfig(_tempDir, """
            {
              "project": { "name": "MyApp" },
              "ado": { "organization": "https://dev.azure.com/myorg", "project": "MyProject" },
              "devRulesFiles": ["does-not-exist.md"]
            }
            """);

        var loader = new ConfigLoader(_tempDir);
        await Assert.ThrowsAsync<ConfigException>(() => loader.LoadAsync());
    }

    private static void WriteConfig(string dir, string json) =>
        File.WriteAllText(Path.Combine(dir, "backlog-2-spec.json"), json, Encoding.UTF8);
}
