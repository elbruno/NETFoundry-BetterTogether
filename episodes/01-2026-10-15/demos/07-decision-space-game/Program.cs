/*
 * Goal: Let a Foundry decision model control a console game.
 * A. Create the same decision client used in demo 01.
 * B. Open the game, which supplies state and four allowed actions.
 * C. Watch decisions become moves; use M for manual play or Q to quit.
 */
using DecisionSpaceGame;
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.Configuration;

// A. Configure values off stream using the episode setup script.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .Build();

using var client = new FoundryDecisionClient(new FoundryDecisionOptions
{
    Endpoint = new Uri(configuration["Foundry:Endpoint"]!),
    Model = configuration["Foundry:Model"]!,
    ApiKey = configuration["Foundry:ApiKey"],
    Timeout = TimeSpan.FromSeconds(10)
});

// B/C. Game.cs defines the prompt and actions; DecisionDriver.cs calls ChooseAsync.
return await GameConsole.RunAsync(client);
