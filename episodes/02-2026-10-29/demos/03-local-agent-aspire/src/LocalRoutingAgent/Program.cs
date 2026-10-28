/*
 * Goal: Run Demo 02's decision agent and export GenAI traces to Aspire.
 * A. Subscribe to the agent and native-decision ActivitySources.
 * B. Invoke the same agent using the developer's Azure CLI identity.
 * C. Print the result and flush telemetry before the console exits.
 */
using Azure.Identity;
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Agents.AI;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SupportRouting;

const string ticket = "I was charged twice for my subscription.";
// Try instead: "The sign-in page rejects my MFA code."
var endpoint = new Uri(Environment.GetEnvironmentVariable("Decision__Endpoint")
    ?? throw new InvalidOperationException("Set the Decision__Endpoint environment variable."));
var deployment = Environment.GetEnvironmentVariable("Decision__Deployment")
    ?? throw new InvalidOperationException("Set the Decision__Deployment environment variable.");

// Aspire supplies the collector endpoint and authentication headers to this process.
using var telemetry = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddService("routing-agent"))
    .AddSource(DecisionTelemetry.AgentSourceName, DecisionTelemetry.SourceName)
    .AddOtlpExporter()
    .Build();

using var decisions = new FoundryDecisionClient(new FoundryDecisionOptions
{
    Endpoint = endpoint,
    Model = deployment,
    Credential = new AzureCliCredential(),
    Timeout = TimeSpan.FromSeconds(45)
});

var agent = new OpenTelemetryAgent(
    new SupportRoutingAgent(decisions, deployment), DecisionTelemetry.AgentSourceName)
{
    EnableSensitiveData = false
};
var response = await agent.RunAsync(ticket);
Console.WriteLine(response.Messages.Single().Text);

if (!telemetry.ForceFlush(10_000))
    throw new TimeoutException("GenAI telemetry did not flush within 10 seconds.");
