// Phase 2: .NET Tools API — HTTP endpoints callable by the Foundry Agent.
// All endpoints return 501 Not Implemented until wired up in Phase 2 implementation.
// TODO Phase 3: RAG — /repo-context will delegate to an Azure AI Search index.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// GET /workitem/{id}
// Will call AdoClient.GetWorkItemAsync and return the work item as JSON.
app.MapGet("/workitem/{id:int}", (int id) =>
    Results.StatusCode(StatusCodes.Status501NotImplemented))
    .WithName("GetWorkItem");

// POST /repo-context
// Body: { "query": "string" }
// Will return relevant repo file snippets via CodebaseContextAgent.
app.MapPost("/repo-context", (RepoContextRequest request) =>
    Results.StatusCode(StatusCodes.Status501NotImplemented))
    .WithName("GetRepoContext");

// POST /spec
// Body: { "workItemId": int, "content": "string" }
// Will save a generated spec to disk.
app.MapPost("/spec", (SaveSpecRequest request) =>
    Results.StatusCode(StatusCodes.Status501NotImplemented))
    .WithName("SaveSpec");

app.Run();

record RepoContextRequest(string Query);
record SaveSpecRequest(int WorkItemId, string Content);
