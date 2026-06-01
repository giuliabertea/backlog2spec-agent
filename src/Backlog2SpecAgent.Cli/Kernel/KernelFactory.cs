using Microsoft.SemanticKernel;

namespace Backlog2SpecAgent.Cli.Kernel;

public sealed class KernelFactory
{
    public Microsoft.SemanticKernel.Kernel Build(
        string endpoint,
        string apiKey,
        string deploymentName)
    {
        return Microsoft.SemanticKernel.Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: deploymentName,
                endpoint: endpoint,
                apiKey: apiKey)
            .Build();
    }
}
