/*
 * Goal: Host the same decision agent locally and in Foundry.
 * A. Explicitly choose developer identity locally, agent-instance identity in Foundry.
 * B. Create the native decision client using external configuration.
 * C. Let the official host implement Responses, readiness, and runtime conventions.
 */
using Azure.AI.AgentServer.Core;
using Azure.Core;
using Azure.Identity;
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Agents.AI.Foundry.Hosting;
using SupportRouting;

var builder = AgentHost.CreateBuilder(args);
var endpoint = builder.Configuration["Decision:Endpoint"]
    ?? throw new InvalidOperationException("Set Decision__Endpoint to the Foundry resource root.");
var deployment = builder.Configuration["Decision:Deployment"]
    ?? throw new InvalidOperationException("Set Decision__Deployment to the decision deployment name.");

TokenCredential credential = string.IsNullOrEmpty(
    Environment.GetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT"))
    ? new AzureCliCredential()
    : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);

using var logs = LoggerFactory.Create(logging => logging.AddConsole());
using var http = new HttpClient(new DecisionTransport(logs.CreateLogger("NativeDecision")));
using var decisions = new FoundryDecisionClient(http, new FoundryDecisionOptions
{
    Endpoint = new Uri(endpoint),
    Model = deployment,
    Credential = credential,
    Timeout = TimeSpan.FromSeconds(45)
});
builder.Services.AddFoundryResponses(new SupportRoutingAgent(decisions));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();

public partial class Program;
