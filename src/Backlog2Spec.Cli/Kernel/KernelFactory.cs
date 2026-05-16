using Microsoft.SemanticKernel;

namespace Backlog2SpecAgent.Cli.Kernel;

public sealed class KernelFactory
{
    public Microsoft.SemanticKernel.Kernel Build(
        string endpoint,
        string apiKey,
        string deploymentName,
        AiEndpointType endpointType = AiEndpointType.AzureOpenAI)
    {
        var builder = Microsoft.SemanticKernel.Kernel.CreateBuilder();

        switch (endpointType)
        {
            case AiEndpointType.AzureOpenAI:
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: deploymentName,
                    endpoint: endpoint,
                    apiKey: apiKey);
                break;

            case AiEndpointType.AzureFoundry:
                builder.AddOpenAIChatCompletion(
                    modelId: deploymentName,
                    apiKey: apiKey,
                    endpoint: new Uri(endpoint));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown endpoint type '{endpointType}'. Valid values are: AzureOpenAI, AzureFoundry.");
        }

        return builder.Build();
    }
}
