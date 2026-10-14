/*
 * Goal: Ask three different decision questions about one synthetic support ticket.
 * A. Create the Foundry client using settings stored outside the source code.
 * B. Define a ticket, choose a queue, score urgency and assess account access.
 * C. Print the signals and an uncalibrated, human-review-only status.
 */
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.Configuration;

// A. Environment variables override this project's user secrets.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .Build();

using var client = new FoundryDecisionClient(new FoundryDecisionOptions
{
    Endpoint = new Uri(configuration["Foundry:Endpoint"]!),
    Model = configuration["Foundry:Model"]!,
    ApiKey = configuration["Foundry:ApiKey"]
});

// B. Each method asks a different question and makes a separate HTTP call.
// Try: "Nobody on our team can sign in, and all customer work is blocked."
// Try: "Which subscription plan should I buy for a team of ten developers?"
var prompt = "I was charged twice for my subscription. I can still use the application.";
var queues = new Dictionary<string, string?>
{
    ["billing"] = "Charges, invoices, refunds and subscription payments",
    ["technical_support"] = "Software errors, outages and account-access problems",
    ["sales"] = "Product selection, plan pricing and buying seats",
    ["other"] = "Requests that do not fit the other queues"
};
var urgencyRubric = new[]
{
    "Low: informational request with no reported impact",
    "Normal: one user affected with a workaround or no immediate blockage",
    "High: a user is blocked from an important task without a workaround",
    "Critical: widespread outage or all users blocked from a core task"
};

var queue = await client.ChooseAsync(
    prompt, "Which support queue should handle this ticket?", queues);
var urgency = await client.ScoreAsync(
    prompt, "Rate the reported urgency using the ordered impact rubric.", urgencyRubric);
var access = await client.AssessAsync(
    prompt, "The ticket reports a problem signing in or accessing the user's account.");

// C. This threshold is teaching policy, not a calibrated automation gate.
var status = queue.Probabilities[queue.Choice] < 0.75 || queue.Choice == "other"
    ? "Human review: examine the suggested queue"
    : "Human review: suggested queue ready to inspect";

Console.WriteLine($"Ticket: {prompt}");
Console.WriteLine($"Choice | Suggested queue: {queue.Choice}");
foreach (var choice in queue.Probabilities)
    Console.WriteLine($"  {choice.Key}: {choice.Value:P1}");
Console.WriteLine($"Provider confidence: {queue.Confidence:P1} (separate signal)");
Console.WriteLine($"Score | Urgency: {urgency.Score:F3} / 3 (weighted index, not a severity label)");
Console.WriteLine($"Assess | Probability of account-access problem: {access.Probability:P1}");
Console.WriteLine($"Uncalibrated status: {status}");
Console.WriteLine("No ticket was routed or changed.");
