/*
 * Goal: Close Better Together with a graphical native-decision game.
 * A. Load local configuration without creating a provider.
 * B. Define lazy Foundry and Ollama clients.
 * C. Open the welcome screen; explicit input starts play.
 */
using DecisionSpaceGame;
using ElBruno.AI.Decisions.Foundry;
using ElBruno.AI.Decisions.Ollama;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly).AddEnvironmentVariables().Build();
using var game = new SpaceGameWindow(new Mission(
    () => new FoundryDecisionClient(new FoundryDecisionOptions
    {
        Endpoint = new Uri(configuration["Foundry:Endpoint"]
            ?? throw new InvalidOperationException("Configure Foundry off stream.")),
        Model = configuration["Foundry:Model"]!,
        ApiKey = configuration["Foundry:ApiKey"],
        Timeout = TimeSpan.FromSeconds(60)
    }),
    () => new OllamaDecisionClient(new OllamaDecisionOptions
    {
        Endpoint = new Uri(configuration["Ollama:Endpoint"] ?? "http://localhost:11434"),
        Model = configuration["Ollama:Model"] ?? "nimble",
        Timeout = TimeSpan.FromSeconds(60)
    }),
    configuration["Foundry:Model"] ?? "Microsoft-Decision-1",
    configuration["Ollama:Model"] ?? "nimble"));
try { game.Run(); }
finally { await game.ShutdownAsync(); }
