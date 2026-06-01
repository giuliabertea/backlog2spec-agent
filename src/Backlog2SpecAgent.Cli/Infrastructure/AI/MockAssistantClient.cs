namespace Backlog2SpecAgent.Cli.Infrastructure.AI;

public sealed class MockAssistantClient : IAssistantClient
{
    public Task<string> RunAsync(string userMessage, CancellationToken ct = default) =>
        Task.FromResult("""
            {
              "goal": "Mock goal: implement the requested feature as described in the ticket.",
              "behaviour": [
                "Accepts valid input and processes it correctly",
                "Returns the expected structured output",
                "Handles invalid input with appropriate error responses"
              ],
              "edgeCases": [
                "Empty or null input is rejected with a validation error",
                "Maximum payload size is enforced"
              ],
              "outOfScope": ["Authentication", "Logging", "Notifications"],
              "filesToChange": [
                { "file": "src/Services/ExampleService.cs", "change": "Add the new method" },
                { "file": "src/Controllers/ExampleController.cs", "change": "Wire up the new endpoint" }
              ]
            }
            """);
}
