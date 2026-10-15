/*
 * Goal: Start the platformer with safe external configuration.
 * A. Load user secrets and environment overrides without printing values.
 * B. Configure lazy Foundry/Ollama clients for the gameplay screen.
 * C. Run the game in manual mode until the user chooses a provider.
 */
using Microsoft.Extensions.Configuration;
using Platformer2D.Core;
using PlatformerDecisions;

internal class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// This creates an instance of your game and calls it's Run() method
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    [STAThread]
    private static void Main(string[] args)
    {
        if (OperatingSystem.IsMacOS())
            Environment.SetEnvironmentVariable("SDL_VIDEO_MAC_FULLSCREEN_SPACES", "0");

        IConfiguration configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly)
            .AddEnvironmentVariables()
            .Build();
        DecisionProviderFactory.Configure(configuration);

        using var game = new Platformer2DGame();
        game.Run();
    }
}