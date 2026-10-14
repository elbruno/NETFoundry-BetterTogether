/*
 * Goal: Run the decision comparison UI with Aspire telemetry.
 * A. Create the local orchestration model.
 * B. Start one Blazor service with its own external configuration.
 * C. Open Aspire's dashboard to inspect paired GenAI traces.
 */
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.DecisionLab_Web>("webfrontend")
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
