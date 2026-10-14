/*
 * Goal: Make one decision with Microsoft-Decision-1 in Foundry.
 * A. Create the client using settings stored outside the source code.
 * B. Define a support message and ask which team should handle it.
 * C. Show the selected team and the model's probability for each option.
 */
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.Configuration;

// A. Use the episode setup script to configure these values before the demo.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .Build();

using var client = new FoundryDecisionClient(new FoundryDecisionOptions
{
    Endpoint = new Uri(configuration["Foundry:Endpoint"]!),
    Model = configuration["Foundry:Model"]!, // Foundry deployment name.
    ApiKey = configuration["Foundry:ApiKey"] // Omit to use your Azure CLI identity.
});

// B. The model selects one of our labels instead of generating a chat response.
// Try: "The application crashes every time I open the settings screen."
// Try: "Which plan should I buy for a team of ten developers?"
var prompt = "I was charged twice for my subscription.";
var choices = new Dictionary<string, string?>
{
    ["billing"] = "Invoices, duplicate charges, and subscription payments",
    ["technical_support"] = "Software errors and account-access problems",
    ["sales"] = "Product selection, pricing, and buying a plan"
};

var result = await client.ChooseAsync(
    prompt,
    "Which team should handle this support message?",
    choices);

// C. Provider confidence is a separate signal, not a guarantee of correctness.
Console.WriteLine($"Message: {prompt}");
Console.WriteLine($"Selected team: {result.Choice}");
foreach (var choice in result.Probabilities)
    Console.WriteLine($"  {choice.Key}: {choice.Value:P1}");
Console.WriteLine($"Provider confidence: {result.Confidence:P1}");
