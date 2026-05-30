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

static AiEndpointType ParseEndpointType(string? raw) => raw switch
{
    null or "" or "AzureOpenAI" => AiEndpointType.AzureOpenAI,
    "AzureFoundry" => AiEndpointType.AzureFoundry,
    _ => throw new InvalidOperationException(
        $"Unknown AzureAI:EndpointType value '{raw}'. Valid values are: AzureOpenAI, AzureFoundry.")
};

bool isMock = args.Contains("--mock");

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
                var tenantId = config["AzureAI:TenantId"]
                    ?? throw new InvalidOperationException("AzureAI:TenantId secret is missing when AzureAI:UseAgent is true.");
                var agentName = config["AzureAI:AgentName"]
                    ?? throw new InvalidOperationException("AzureAI:AgentName secret is missing when AzureAI:UseAgent is true.");
                var toolsBaseUrl = config["AzureAI:ToolsBaseUrl"]
                    ?? throw new InvalidOperationException("AzureAI:ToolsBaseUrl secret is missing when AzureAI:UseAgent is true.");
                var toolsApiKey = config["AzureAI:ToolsApiKey"]
                    ?? throw new InvalidOperationException("AzureAI:ToolsApiKey secret is missing when AzureAI:UseAgent is true.");
                services.AddSingleton<IFoundryAgentClient>(sp =>
                    new FoundryAgentClient(projectEndpoint, tenantId, agentName, toolsBaseUrl, toolsApiKey,
                        sp.GetRequiredService<ILogger<FoundryAgentClient>>()));
                services.AddSingleton<ISpecGeneratorAgent, FoundrySpecGeneratorAgent>();
            }
            else
            {
                var endpoint = config["AzureAI:Endpoint"] ?? throw new InvalidOperationException("AzureAI:Endpoint secret is missing.");
                var deploymentName = config["AzureAI:DeploymentName"] ?? throw new InvalidOperationException("AzureAI:DeploymentName secret is missing.");
                var endpointType = ParseEndpointType(config["AzureAI:EndpointType"]);
                var kernel = new KernelFactory().Build(endpoint, apiKey, deploymentName, endpointType);

                services.AddSingleton(kernel);
                services.AddSingleton<IEnrichmentAgent, EnrichmentAgent>();
                services.AddSingleton<IKeywordExtractor>(sp =>
                    new LlmKeywordExtractor(sp.GetRequiredService<Microsoft.SemanticKernel.Kernel>(), sp.GetRequiredService<ILogger<LlmKeywordExtractor>>()));
                services.AddSingleton<ICodebaseContextAgent>(sp =>
                    new CodebaseContextAgent(pat, sp.GetRequiredService<IKeywordExtractor>(), sp.GetRequiredService<ILogger<CodebaseContextAgent>>()));
                services.AddSingleton<ISpecGeneratorAgent, SpecGeneratorAgent>();
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
