using System.CommandLine;
using Backlog2SpecAgent.Cli.Ado;
using Backlog2SpecAgent.Cli.Agents;
using Backlog2SpecAgent.Cli.Commands;
using Backlog2SpecAgent.Cli.Config;
using Backlog2SpecAgent.Cli.Infrastructure.AI;
using Backlog2SpecAgent.Cli.Kernel;
using Backlog2SpecAgent.Cli.Output;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

bool isMock = args.Contains("--mock");

// Load project config file early — toolsApi.baseUrl is needed before DI setup.
BacklogConfig fileConfig;
if (isMock)
{
    fileConfig = new BacklogConfig();
}
else
{
    try
    {
        fileConfig = await new ConfigLoader().LoadAsync();
    }
    catch (ConfigException ex)
    {
        Console.Error.WriteLine($"Configuration error: {ex.Message}");
        return 1;
    }
}

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((_, cfg) =>
    {
        cfg.AddUserSecrets<Program>();
    })
    .ConfigureServices((ctx, services) =>
    {
        var config = ctx.Configuration;

        services.AddSingleton<ConfigLoader>();
        services.AddSingleton<IOutputRenderer, OutputRenderer>();
        services.AddSingleton<SpecCommand>();

        if (isMock)
        {
            services.AddSingleton<IAdoClient, MockAdoClient>();
            services.AddSingleton<ISpecGeneratorAgent, MockSpecGeneratorAgent>();
            services.AddSingleton<IHierarchyFetcher, MockHierarchyFetcher>();
        }
        else
        {
            var apiKey = config["AzureAI:ApiKey"] ?? throw new InvalidOperationException("AzureAI:ApiKey secret is missing.");
            var pat = config["Ado:Pat"] ?? throw new InvalidOperationException("Ado:Pat secret is missing.");
            var useAgent = config.GetValue<bool>("AzureAI:UseAgent");

            services.AddSingleton<IAdoClient>(sp =>
                new AdoClient(sp.GetRequiredService<ConfigLoader>(), pat));

            if (useAgent)
            {
                var projectEndpoint = config["AzureAI:ProjectEndpoint"]
                    ?? throw new InvalidOperationException("AzureAI:ProjectEndpoint secret is missing when AzureAI:UseAgent is true.");
                var assistantId = config["AzureAI:AssistantId"]
                    ?? throw new InvalidOperationException("AzureAI:AssistantId secret is missing when AzureAI:UseAgent is true.");

                var toolsBaseUrl = fileConfig.ToolsApi.BaseUrl;
                if (string.IsNullOrWhiteSpace(toolsBaseUrl))
                    throw new InvalidOperationException("toolsApi.baseUrl is missing in backlog-2-spec.json when AzureAI:UseAgent is true.");

                var toolsApiKey = config["AzureAI:ToolsApiKey"]
                    ?? throw new InvalidOperationException("AzureAI:ToolsApiKey secret is missing when AzureAI:UseAgent is true.");
                var searchEndpoint = config["AzureSearch:Endpoint"]
                    ?? throw new InvalidOperationException("AzureSearch:Endpoint secret is missing when AzureAI:UseAgent is true.");
                var searchApiKey = config["AzureSearch:ApiKey"]
                    ?? throw new InvalidOperationException("AzureSearch:ApiKey secret is missing when AzureAI:UseAgent is true.");
                var searchIndexName = config["AzureSearch:IndexName"] ?? "codebase-chunks";

                services.AddSingleton<IAssistantClient>(sp =>
                    new AssistantClient(projectEndpoint, apiKey, assistantId,
                        sp.GetRequiredService<ILogger<AssistantClient>>()));
                services.AddSingleton<ISpecGeneratorAgent>(sp =>
                    new AssistantSpecGeneratorAgent(
                        sp.GetRequiredService<IAssistantClient>(),
                        sp.GetRequiredService<ConfigLoader>(),
                        toolsBaseUrl, toolsApiKey,
                        searchEndpoint, searchApiKey, searchIndexName,
                        sp.GetRequiredService<ILogger<AssistantSpecGeneratorAgent>>()));
                services.AddSingleton<IHierarchyFetcher>(
                    _ => new ToolsApiHierarchyFetcher(toolsBaseUrl, toolsApiKey));
            }
            else
            {
                var endpoint = config["AzureAI:Endpoint"] ?? throw new InvalidOperationException("AzureAI:Endpoint secret is missing.");
                var deploymentName = config["AzureAI:DeploymentName"] ?? throw new InvalidOperationException("AzureAI:DeploymentName secret is missing.");
                var kernel = new KernelFactory().Build(endpoint, apiKey, deploymentName);

                services.AddSingleton(kernel);
                services.AddSingleton<IEnrichmentAgent, EnrichmentAgent>();
                services.AddSingleton<IKeywordExtractor>(sp =>
                    new LlmKeywordExtractor(sp.GetRequiredService<Microsoft.SemanticKernel.Kernel>(), sp.GetRequiredService<ILogger<LlmKeywordExtractor>>()));
                services.AddSingleton<ICodebaseContextAgent>(sp =>
                    new CodebaseContextAgent(pat, sp.GetRequiredService<IKeywordExtractor>(), sp.GetRequiredService<ILogger<CodebaseContextAgent>>()));
                services.AddSingleton<ISpecGeneratorAgent, SpecGeneratorAgent>();
                services.AddSingleton<IHierarchyFetcher>(sp =>
                    new AdoHierarchyFetcher(sp.GetRequiredService<IAdoClient>()));
            }
        }

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    })
    .Build();

if (isMock)
{
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MockMode");
    logger.LogInformation("[MOCK MODE ENABLED]");
}

var specCommand = host.Services.GetRequiredService<SpecCommand>();
var rootCommand = new RootCommand("backlog-2-spec — AI-powered spec generator for Azure DevOps work items");
rootCommand.AddCommand(specCommand);

return await rootCommand.InvokeAsync(args);
