#:sdk Aspire.AppHost.Sdk@13.6.1
#:property AspireUseCliBundle=true
#:project ./src/LocalRoutingAgent/LocalRoutingAgent.csproj

/*
 * Goal: Run the local decision agent with Aspire's dashboard.
 * A. Reuse an existing model through external configuration.
 * B. Start one console resource with Aspire-managed OTLP configuration.
 * C. Keep prompt and response content out of telemetry.
 */

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject("routing-agent", "./src/LocalRoutingAgent/LocalRoutingAgent.csproj")
    .WithEnvironment("Decision__Endpoint", builder.Configuration["Decision:Endpoint"]
        ?? throw new InvalidOperationException("Set Decision__Endpoint before starting Aspire."))
    .WithEnvironment("Decision__Deployment", builder.Configuration["Decision:Deployment"]
        ?? throw new InvalidOperationException("Set Decision__Deployment before starting Aspire."))
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false");

builder.Build().Run();
