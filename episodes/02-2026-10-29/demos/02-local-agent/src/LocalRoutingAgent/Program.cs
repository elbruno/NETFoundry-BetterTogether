/*
 * Goal: Run the support-routing agent in-process from a local console app.
 * A. Build the same native Foundry decision client as Demo 01.
 * B. Pass one ticket to the Microsoft Agent Framework agent.
 * C. Print the agent's typed JSON output without starting an HTTP host.
 */
using Azure.Identity;
using ElBruno.AI.Decisions.Foundry;
using SupportRouting;

const string ticket = "I was charged twice for my subscription.";
// Try instead: "The sign-in page rejects my MFA code."
var endpoint = new Uri(Environment.GetEnvironmentVariable("Decision__Endpoint")
    ?? throw new InvalidOperationException("Set the Decision__Endpoint environment variable."));
var deployment = Environment.GetEnvironmentVariable("Decision__Deployment")
    ?? throw new InvalidOperationException("Set the Decision__Deployment environment variable.");

// The local stage authenticates with the developer's Azure CLI identity.
using var decisions = new FoundryDecisionClient(new FoundryDecisionOptions
{
    Endpoint = endpoint,
    Model = deployment,
    Credential = new AzureCliCredential(),
    Timeout = TimeSpan.FromSeconds(45)
});

// Run the agent directly. There is no remote agent endpoint in this stage.
var response = await new SupportRoutingAgent(decisions).RunAsync(ticket);
Console.WriteLine(response.Messages.Single().Text);
