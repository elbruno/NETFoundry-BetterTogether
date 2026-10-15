/*
 * Goal: Run responsive Spectre gameplay with optional one-mission auto-start.
 * A. Create providers lazily, keep M/F/O/N controls and one request in flight.
 * B. Pause the AI world during inference, animate four ticks, and track the mission.
 * C. Cancel/drain work before printing an honest outcome and statistics report.
 */
using System.Diagnostics;
using ElBruno.AI.Decisions;
using Spectre.Console;

namespace DecisionSpaceGame;

public static class GameConsole
{
    public static Task<int> RunAsync(IDecisionClient client, bool headless = false, double seconds = 90) =>
        RunAsync(() => client, null, headless, seconds, ownsClients: false,
            launch: new LaunchOptions("foundry", true));

    public static async Task<int> RunAsync(Func<IDecisionClient> foundry,
        Func<IDecisionClient>? ollama, bool headless = false, double seconds = double.PositiveInfinity,
        bool ownsClients = true, LaunchOptions? launch = null,
        string foundryModel = "Microsoft-Decision-1", string ollamaModel = "nimble:latest")
    {
        launch ??= new();
        if (!headless && (Console.IsInputRedirected || Console.IsOutputRedirected))
        {
            Console.Error.WriteLine("Interactive play needs a real terminal. Run dotnet run in a terminal.");
            return 2;
        }
        var clients = new Dictionary<string, IDecisionClient>();
        var driver = new DecisionDriver(TimeSpan.FromSeconds(60), Game.ActionTicks);
        var game = new Game(42);
        using var shutdown = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; shutdown.Cancel(); };
        Console.CancelKeyPress += cancel;
        var watch = new Stopwatch();
        TimeSpan missionElapsed = TimeSpan.Zero;
        string? outcome = null;
        double nextTickMs = Game.StepMilliseconds, nextRenderMs = 0;
        double wallLimit = launch.AutoFinish ? Math.Min(seconds, LaunchOptions.MaxAutoSeconds) : seconds;
        bool quit = false, paused = false;
        try
        {
            watch.Start();
            Select(launch.Provider);
            if (headless) await Loop(null);
            else
            {
                AnsiConsole.Clear();
                await AnsiConsole.Live(GamePresentation.Screen(game, driver, watch.Elapsed, AnsiConsole.Profile.Width, null))
                    .AutoClear(true).StartAsync(async context => await Loop(context));
            }
            outcome ??= shutdown.IsCancellationRequested ? "cancelled by Ctrl+C" : "quit";
            if (missionElapsed == TimeSpan.Zero) missionElapsed = watch.Elapsed;
            // Drain a cancelled call before taking the final statistics snapshot.
            await driver.DisposeAsync();
            if (headless)
                foreach (var (metric, value) in GamePresentation.SummaryRows(game, driver, missionElapsed, outcome))
                    Console.WriteLine($"{metric}: {value}");
            else AnsiConsole.Write(GamePresentation.Summary(game, driver, missionElapsed, outcome));
            return driver.Failed || (headless && driver.Provider != "manual" && driver.Statistics.Accepted == 0) ? 1 : 0;

            async Task Loop(LiveDisplayContext? display)
            {
                string? lastStatus = null;
                DecisionAttempt? lastAttempt = null;
                while (!quit && !shutdown.IsCancellationRequested)
                {
                    if (!headless)
                    {
                        while (Console.KeyAvailable)
                        {
                            ConsoleKey key = Console.ReadKey(intercept: true).Key;
                            switch (key)
                            {
                                case ConsoleKey.Q: case ConsoleKey.Escape: quit = true; break;
                                case ConsoleKey.M when outcome is null && !paused: Select("manual"); break;
                                case ConsoleKey.F when outcome is null && !paused: Select("foundry"); break;
                                case ConsoleKey.O when outcome is null && !paused: Select("ollama"); break;
                                case ConsoleKey.P when outcome is null:
                                    paused = !paused;
                                    if (paused) driver.Statistics.RecordPause();
                                    nextTickMs = watch.Elapsed.TotalMilliseconds + Game.StepMilliseconds;
                                    break;
                                case ConsoleKey.N:
                                    driver.ResetStatistics();
                                    game = new Game(42);
                                    Select("manual");
                                    watch.Restart();
                                    missionElapsed = TimeSpan.Zero;
                                    outcome = null;
                                    paused = false;
                                    nextTickMs = Game.StepMilliseconds;
                                    nextRenderMs = 0;
                                    break;
                                default:
                                    if (outcome is null && !paused && driver.Provider == "manual" &&
                                        Actions.TryKey(key, out GameAction action)) game.Apply(action);
                                    break;
                            }
                        }
                    }
                    if (quit || shutdown.IsCancellationRequested) break;
                    if (outcome is null)
                    {
                        if (watch.Elapsed.TotalSeconds >= wallLimit)
                            Finish("wall-time limit");
                        else if (launch.AutoFinish && game.Tick >= LaunchOptions.MaxAutoTicks)
                            Finish("simulation limit");
                        else if (!paused)
                        {
                            driver.Update(game);
                            // Do not replay accumulated wall ticks after inference.
                            if (ShouldAdvance(driver, game) && watch.Elapsed.TotalMilliseconds >= nextTickMs)
                            {
                                game.Step();
                                nextTickMs = watch.Elapsed.TotalMilliseconds + Game.StepMilliseconds;
                            }
                            else if (!ShouldAdvance(driver, game))
                                nextTickMs = watch.Elapsed.TotalMilliseconds + Game.StepMilliseconds;
                            if (game.Finished) Finish(game.Lives > 0 ? "victory" : "defeat");
                            else if (launch.AutoFinish && driver.Failed) Finish("provider failure");
                        }
                    }
                    if (headless && (lastStatus != driver.Status || lastAttempt != driver.LastAttempt))
                    {
                        Console.WriteLine($"tick={game.Tick} score={game.Score} {driver.Status}");
                        lastStatus = driver.Status;
                        lastAttempt = driver.LastAttempt;
                    }
                    if (display is not null && watch.Elapsed.TotalMilliseconds >= nextRenderMs)
                    {
                        display.UpdateTarget(GamePresentation.Screen(game, driver,
                            outcome is null ? watch.Elapsed : missionElapsed, AnsiConsole.Profile.Width, outcome, paused));
                        display.Refresh();
                        nextRenderMs = watch.Elapsed.TotalMilliseconds + 100;
                    }
                    if (outcome is not null && (launch.AutoFinish || headless)) break;
                    await Task.Delay(10, shutdown.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                }
            }

            void Finish(string result)
            {
                outcome = result;
                missionElapsed = watch.Elapsed;
                // Interactive mode keeps the final battlefield visible until N or Q.
            }

            void Select(string name)
            {
                string model = name == "manual" ? "keyboard" : name == "foundry" ? foundryModel : ollamaModel;
                if (name == "manual") { driver.Switch(game, name, model, null); return; }
                try
                {
                    if (!clients.TryGetValue(name, out var selected))
                    {
                        selected = (name == "foundry" ? foundry : ollama
                            ?? throw new InvalidOperationException("Ollama is not configured."))();
                        clients.Add(name, selected);
                    }
                    driver.Switch(game, name, model, selected);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    driver.Switch(game, name, model, null,
                        $"Cannot configure {name}. Check settings off stream; press M to play.");
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
            await driver.DisposeAsync();
            if (ownsClients)
                foreach (var client in clients.Values.OfType<IDisposable>()) client.Dispose();
        }
    }

    public static bool ShouldAdvance(DecisionDriver driver, Game game, bool paused = false) =>
        !paused && !game.Finished && (driver.Provider == "manual" || (!driver.HasPendingRequest && !driver.Failed));
}
