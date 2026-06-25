using Backlog2SpecAgent.Cli.Config;
using Microsoft.Extensions.Configuration;

namespace Backlog2SpecAgent.Tests;

public class ConfigLoaderTests
{
    private static ConfigLoader BuildLoader(Dictionary<string, string?> values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Fact]
    public async Task LoadAsync_ValidConfiguration_ReturnsPopulatedConfig()
    {
        var loader = BuildLoader(new()
        {
            ["Project:Name"]        = "MyApp",
            ["Project:Language"]    = "C#",
            ["Ado:Organization"]    = "https://dev.azure.com/myorg",
            ["Ado:Project"]         = "MyProject",
            ["ToolsApi:BaseUrl"]    = "https://tools.example.net"
        });

        var config = await loader.LoadAsync();

        Assert.Equal("MyApp", config.Project.Name);
        Assert.Equal("C#", config.Project.Language);
        Assert.Equal("https://dev.azure.com/myorg", config.Ado.Organization);
        Assert.Equal("MyProject", config.Ado.Project);
        Assert.Equal("https://tools.example.net", config.ToolsApi.BaseUrl);
    }

    [Fact]
    public async Task LoadAsync_MissingAdoOrganization_ThrowsConfigException()
    {
        var loader = BuildLoader(new()
        {
            ["Project:Name"]     = "MyApp",
            ["Ado:Organization"] = "",
            ["Ado:Project"]      = "MyProject"
        });

        await Assert.ThrowsAsync<ConfigException>(() => loader.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_MissingProjectName_ThrowsConfigException()
    {
        var loader = BuildLoader(new()
        {
            ["Project:Name"]     = "",
            ["Ado:Organization"] = "https://dev.azure.com/myorg",
            ["Ado:Project"]      = "MyProject"
        });

        await Assert.ThrowsAsync<ConfigException>(() => loader.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_WithInlineDevRules_UsesContentDirectly()
    {
        var loader = BuildLoader(new()
        {
            ["Project:Name"]     = "MyApp",
            ["Ado:Organization"] = "https://dev.azure.com/myorg",
            ["Ado:Project"]      = "MyProject",
            ["DevRules:Content"] = "No AutoMapper.\n\nUse Result<T>."
        });

        var config = await loader.LoadAsync();

        Assert.Equal("No AutoMapper.\n\nUse Result<T>.", config.DevRulesContent);
    }

    [Fact]
    public async Task LoadAsync_DevRulesFileMissing_ThrowsConfigException()
    {
        var loader = BuildLoader(new()
        {
            ["Project:Name"]     = "MyApp",
            ["Ado:Organization"] = "https://dev.azure.com/myorg",
            ["Ado:Project"]      = "MyProject",
            ["DevRules:Files:0"] = "does-not-exist.md"
        });

        await Assert.ThrowsAsync<ConfigException>(() => loader.LoadAsync());
    }
}
