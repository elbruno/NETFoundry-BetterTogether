/*
 * Goal: Keep rendering and keyboard plumbing separate from the teaching entry point.
 * A. Start the seeded game with the supplied decision client.
 * B. Run a responsive simulation with keyboard input.
 * C. Display decisions and summarize the bounded run.
 * Inputs: A decision client and optional keyboard actions.
 * Output: A game board or headless log with real probabilities and latency.
 * Read next: DecisionDriver.cs owns inference scheduling and result validation.
 * Boundary: Rendering never waits synchronously for network inference.
 */
using System.Diagnostics;
using System.Text;
using DecisionSpaceGame;
using ElBruno.AI.Decisions;

namespace DecisionSpaceGame;

public static class GameConsole
{
    public static async Task<int> RunAsync(IDecisionClient client, bool headless = false, double seconds = 90)
    {
        if (!headless && (Console.IsInputRedirected || Console.IsOutputRedirected))
        {
            Console.Error.WriteLine("Interactive play needs a real terminal.");
            return 2;
        }
        // A. The entry point owns the client; the renderer owns no credentials.
        await using var driver = new DecisionDriver(TimeSpan.FromSeconds(10),
            5000 / Game.StepMilliseconds);
        var game = new Game(42);
        using var shutdown = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; shutdown.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            // B. The game clock continues while the driver waits for the model.
            Select("foundry");
            Console.WriteLine($"Decision Space Game | seed=42 | {driver.Provider} / {driver.Model}");
            var watch = Stopwatch.StartNew();
            double nextTickMs = Game.StepMilliseconds, nextRenderMs = 0;
            string? lastHeadlessStatus = null;
            DecisionAttempt? lastHeadlessAttempt = null;
            bool quit = false;
            while (!quit && !shutdown.IsCancellationRequested && !game.Finished && watch.Elapsed.TotalSeconds < seconds)
            {
                if (!headless)
                {
                    while (Console.KeyAvailable)
                    {
                        ConsoleKey key = Console.ReadKey(intercept: true).Key;
                        switch (key)
                        {
                            case ConsoleKey.Q: case ConsoleKey.Escape: quit = true; break;
                            case ConsoleKey.M: Select("manual"); break;
                            case ConsoleKey.F: Select("foundry"); break;
                            case ConsoleKey.R: Select("replay"); break;
                            default:
                                if (driver.Provider == "manual" && Actions.TryKey(key, out GameAction action))
                                    game.Apply(action);
                                break;
                        }
                    }
                }
                while (watch.Elapsed.TotalMilliseconds >= nextTickMs && !game.Finished)
                {
                    game.Step();
                    nextTickMs += Game.StepMilliseconds;
                }
                driver.Update(game);
                if (headless && (lastHeadlessStatus != driver.Status || lastHeadlessAttempt != driver.LastAttempt))
                {
                    Console.WriteLine($"tick={game.Tick} {driver.Status} {Describe(driver.LastAttempt)}");
                    lastHeadlessStatus = driver.Status;
                    lastHeadlessAttempt = driver.LastAttempt;
                }
                else if (!headless && watch.Elapsed.TotalMilliseconds >= nextRenderMs)
                {
                    Render(game, driver);
                    nextRenderMs = watch.Elapsed.TotalMilliseconds + 100;
                }
                await Task.Delay(10, shutdown.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
            // C. A failed provider stays visible; we never substitute synthetic play.
            Console.WriteLine($"\nFINAL provider={driver.Provider} model={driver.Model} tick={game.Tick} " +
                $"score={game.Score} lives={game.Lives} accepted={driver.Accepted} rejected={driver.Rejected} " +
                $"outcome={(game.Finished ? game.Lives > 0 ? "victory" : "game-over" : "duration/quit")} status={driver.Status}");
            if (driver.Failed) return 1;
            if (headless && driver.Provider != "manual" && driver.Accepted == 0)
            {
                Console.Error.WriteLine("No AI/replay decision applied within the run.");
                return 1;
            }
            return 0;

            void Select(string name)
            {
                IDecisionClient? selected = name switch
                {
                    "manual" => null,
                    "replay" => new ReplayClient(),
                    _ => client
                };
                string label = name == "replay" ? "OFFLINE scripted actions (NOT a model)" :
                    name == "manual" ? "keyboard" : "Microsoft-Decision-1";
                driver.Switch(game, name, label, selected);
            }
        }
        finally { Console.CancelKeyPress -= cancel; }
    }

    static string Describe(DecisionAttempt? attempt)
    {
        if (attempt is null) return "";
        if (attempt.Error is not null) return $"ERROR {attempt.Error} latencyMs={attempt.LatencyMs:F0}";
        var decision = attempt.Decision!;
        return $"choice={decision.Choice} selectedP={decision.Probabilities[decision.Choice]:P1} " +
            $"confidence={decision.Confidence:P1} latencyMs={attempt.LatencyMs:F0} " +
            $"distribution=[{string.Join(", ", decision.Probabilities.Select(p => $"{p.Key}:{p.Value:F3}"))}]";
    }

    static void Render(Game game, DecisionDriver driver)
    {
        var cells = new char[Game.Height, Game.Width];
        for (int y = 0; y < Game.Height; y++)
            for (int x = 0; x < Game.Width; x++) cells[y, x] = ' ';
        foreach (Position p in game.Enemies) cells[p.Y, p.X] = 'W';
        foreach (Position p in game.Shots) cells[p.Y, p.X] = '|';
        foreach (Position p in game.HostileShots) cells[p.Y, p.X] = '!';
        cells[Game.PlayerY, game.PlayerX] = '^';
        var lines = new List<string>
    {
        $"DECISION SPACE | provider={driver.Provider}",
        $"model={driver.Model}",
        $"tick={game.Tick} score={game.Score} lives={game.Lives} active={game.ActiveAction}",
        "A/Left D/Right Space S | M manual F Foundry R OFFLINE | Q quit",
        driver.Status + (driver.Failed ? " Select M for manual or reselect provider to retry." : ""),
    };
        if (driver.LastAttempt is { Decision: { } decision } attempt)
        {
            lines.Add($"choice={decision.Choice} latencyMs={attempt.LatencyMs:F0}");
            lines.Add($"selectedP={decision.Probabilities[decision.Choice]:P1} confidence={decision.Confidence:P1}");
            lines.Add("distribution (provider values):");
            foreach (var probability in decision.Probabilities)
                lines.Add($"  {probability.Key,-10} {probability.Value:P1}");
        }
        else if (driver.LastAttempt is { Error: { } error }) lines.Add($"ERROR {error}");
        lines.Add("+" + new string('-', Game.Width) + "+");
        for (int y = 0; y < Game.Height; y++)
        {
            var row = new StringBuilder("|");
            for (int x = 0; x < Game.Width; x++) row.Append(cells[y, x]);
            lines.Add(row.Append('|').ToString());
        }
        lines.Add("+" + new string('-', Game.Width) + "+");
        int width = Math.Max(1, Console.WindowWidth - 1);
        int height = Console.WindowHeight;
        Console.SetCursorPosition(0, 0);
        for (int i = 0; i < height - 1; i++)
        {
            string line = i < lines.Count ? lines[i] : "";
            Console.WriteLine((line.Length > width ? line[..width] : line).PadRight(width));
        }
    }
}
