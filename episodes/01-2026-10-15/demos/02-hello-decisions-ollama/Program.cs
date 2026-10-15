/*
 * Goal: Make the same decision as demo 01 using a local Ollama model.
 * A. Create the local decision client using external configuration.
 * B. Define a support message and ask which team should handle it.
 * C. Show the selected team and the model's probability for each option.
 */
using ElBruno.AI.Decisions.Ollama;
using Microsoft.Extensions.Configuration;

// A. Ollama runs locally; this native decision endpoint does not need an API key.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .Build();

using var client = new OllamaDecisionClient(new OllamaDecisionOptions
{
    Endpoint = new Uri(configuration["Ollama:Endpoint"]!),
    Model = configuration["Ollama:Model"]!
});

// B. Only the provider changes. The message, choices, and question stay the same.
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
// Local SystemOne may omit usage; missing is not zero.
Console.WriteLine($"Tokens: input={result.Usage?.InputTokens?.ToString() ?? "not reported"}, output={result.Usage?.OutputTokens?.ToString() ?? "not reported"}, total={result.Usage?.TotalTokens?.ToString() ?? "not reported"}");
