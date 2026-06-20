using Backlog2SpecAgent.Cli.Agents;
using Backlog2SpecAgent.Cli.Infrastructure.AI;
using Backlog2SpecAgent.Cli.Models;

namespace Backlog2SpecAgent.Tests;

public class SpecGeneratorAgentTests
{
    // --- MockSpecGeneratorAgent ---

    [Fact]
    public async Task MockSpecGeneratorAgent_ReturnsNonEmptyGoal()
    {
        var agent = new MockSpecGeneratorAgent();
        var spec = await agent.GenerateAsync(1);
        Assert.False(string.IsNullOrWhiteSpace(spec.Goal));
    }

    [Fact]
    public async Task MockSpecGeneratorAgent_ReturnsBehaviourItems()
    {
        var agent = new MockSpecGeneratorAgent();
        var spec = await agent.GenerateAsync(1);
        Assert.NotEmpty(spec.Behaviour);
    }

    [Fact]
    public async Task MockSpecGeneratorAgent_ReturnsEdgeCases()
    {
        var agent = new MockSpecGeneratorAgent();
        var spec = await agent.GenerateAsync(1);
        Assert.NotEmpty(spec.EdgeCases);
    }

    [Fact]
    public async Task MockSpecGeneratorAgent_ReturnsFilesToChange()
    {
        var agent = new MockSpecGeneratorAgent();
        var spec = await agent.GenerateAsync(1);
        Assert.NotEmpty(spec.FilesToChange);
    }

    [Fact]
    public async Task MockSpecGeneratorAgent_FilesToChange_HaveNonEmptyEvidence()
    {
        var agent = new MockSpecGeneratorAgent();
        var spec = await agent.GenerateAsync(1);
        Assert.All(spec.FilesToChange, f => Assert.False(string.IsNullOrWhiteSpace(f.Evidence)));
    }

    [Fact]
    public async Task MockSpecGeneratorAgent_FilesToChange_HaveConfidence()
    {
        var agent = new MockSpecGeneratorAgent();
        var spec = await agent.GenerateAsync(1);
        Assert.All(spec.FilesToChange, f => Assert.False(string.IsNullOrWhiteSpace(f.Confidence)));
    }

    [Fact]
    public async Task MockSpecGeneratorAgent_IsDeterministic()
    {
        var agent = new MockSpecGeneratorAgent();
        var a = await agent.GenerateAsync(1);
        var b = await agent.GenerateAsync(99);
        Assert.Equal(a.Goal, b.Goal);
        Assert.Equal(a.OutOfScope, b.OutOfScope);
    }

    [Fact]
    public async Task MockSpecGeneratorAgent_RespectsCancellation()
    {
        var agent = new MockSpecGeneratorAgent();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        // MockSpecGeneratorAgent is synchronous internally — it should complete fine even
        // with a cancelled token because it never yields to the scheduler.
        var spec = await agent.GenerateAsync(1, cts.Token);
        Assert.NotNull(spec);
    }

    // --- MockAssistantClient ---

    [Fact]
    public async Task MockAssistantClient_ReturnsValidJson()
    {
        var client = new MockAssistantClient();
        var raw = await client.RunAsync("any input");
        Assert.False(string.IsNullOrWhiteSpace(raw));
        using var doc = System.Text.Json.JsonDocument.Parse(raw);
        Assert.True(doc.RootElement.TryGetProperty("goal", out _));
    }

    [Fact]
    public async Task MockAssistantClient_FilesToChange_HaveEvidenceAndConfidence()
    {
        var client = new MockAssistantClient();
        var raw = await client.RunAsync("any input");
        using var doc = System.Text.Json.JsonDocument.Parse(raw);
        Assert.True(doc.RootElement.TryGetProperty("filesToChange", out var files));
        foreach (var file in files.EnumerateArray())
        {
            Assert.True(file.TryGetProperty("evidence", out var ev));
            Assert.False(string.IsNullOrWhiteSpace(ev.GetString()));
            Assert.True(file.TryGetProperty("confidence", out var conf));
            Assert.False(string.IsNullOrWhiteSpace(conf.GetString()));
        }
    }

    [Fact]
    public async Task MockAssistantClient_IsDeterministic()
    {
        var client = new MockAssistantClient();
        var a = await client.RunAsync("msg1");
        var b = await client.RunAsync("msg2");
        Assert.Equal(a, b);
    }
}
