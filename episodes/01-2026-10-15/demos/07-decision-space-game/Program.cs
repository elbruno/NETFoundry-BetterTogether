/*
 * Goal: Play manually or hand the ship to either native decision provider.
 * A. Load external settings, without requiring cloud access for manual play.
 * B. Create a provider only when F or O is pressed.
 * C. Start manually, or auto-start one bounded run with --foundry / --ollama.
 */
using DecisionSpaceGame;
using ElBruno.AI.Decisions.Foundry;
using ElBruno.AI.Decisions.Ollama;
using Microsoft.Extensions.Configuration;

// A. Configure values off stream using the episode setup script.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .Build();

// B. These factories run only when their provider is selected, not at launch.
if (args is ["--help"])
{
    Console.WriteLine(LaunchOptions.Usage);
    return 0;
}
LaunchOptions launch;
try { launch = LaunchOptions.Parse(args); }
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"{ex.Message}\n{LaunchOptions.Usage}");
    return 2;
}
return await GameConsole.RunAsync(
    foundry: () => new FoundryDecisionClient(new FoundryDecisionOptions
    {
        Endpoint = new Uri(configuration["Foundry:Endpoint"]
            ?? throw new InvalidOperationException("Configure Foundry using the episode setup script.")),
        Model = configuration["Foundry:Model"]!,
        ApiKey = configuration["Foundry:ApiKey"],
        Timeout = TimeSpan.FromSeconds(60)
    }),
    ollama: () => new OllamaDecisionClient(new OllamaDecisionOptions
    {
        Endpoint = new Uri(configuration["Ollama:Endpoint"] ?? "http://localhost:11434"),
        Model = configuration["Ollama:Model"] ?? "nimble:latest",
        Timeout = TimeSpan.FromSeconds(60)
    }),
    launch: launch,
    foundryModel: configuration["Foundry:Model"] ?? "Microsoft-Decision-1",
    ollamaModel: configuration["Ollama:Model"] ?? "nimble:latest");
