using System.Diagnostics;
using System.Text;
using DecisionSpaceGame;
using Microsoft.Extensions.Configuration;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    Cli cli;
    try { cli = Cli.Parse(args); }
    catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); return 2; }
    if (cli.Help) { Console.WriteLine(Cli.HelpText); return 0; }
    if (!cli.Headless && !cli.Probe && (Console.IsInputRedirected || Console.IsOutputRedirected))
    {
        Console.Error.WriteLine("Interactive mode needs a terminal. Use --headless or --probe.");
        return 2;
    }
    var config = new ConfigurationBuilder().AddUserSecrets<Providers>(optional: true).AddEnvironmentVariables().Build();
    using var providers = new Providers(config, TimeSpan.FromSeconds(cli.TimeoutSeconds));
    await using var driver = new DecisionDriver(TimeSpan.FromSeconds(cli.TimeoutSeconds),
        (int)(cli.MaxAgeSeconds * 1000 / Game.StepMilliseconds));
    var game = new Game(cli.Seed);
    using var shutdown = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; shutdown.Cancel(); };
    Console.CancelKeyPress += cancel;
    try
    {
        if (cli.Probe)
        {
            var provider = providers.Get(cli.Provider);
            Console.WriteLine($"PROBE provider={cli.Provider} model={provider.Model} seed={cli.Seed} fixed tick=0");
            Console.WriteLine($"snapshot={game.Snapshot()}");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(cli.TimeoutSeconds));
            DecisionAttempt attempt = await DecisionCalls.ChooseAsync(provider.Client!, game.Snapshot(), timeout.Token);
            Console.WriteLine(Describe(attempt));
            if (attempt.Error is not null || timeout.IsCancellationRequested) return 1;
            if (!DecisionDriver.IsLegal(attempt.Decision!, game, out _))
            {
                Console.Error.WriteLine("Probe returned an illegal decision."); return 1;
            }
            return 0;
        }
        Select(cli.Provider);
        Console.WriteLine($"Decision Space Game | seed={cli.Seed} | {driver.Provider} / {driver.Model}");
        var watch = Stopwatch.StartNew();
        double nextTickMs = Game.StepMilliseconds, nextRenderMs = 0;
        string? lastHeadlessStatus = null;
        DecisionAttempt? lastHeadlessAttempt = null;
        bool quit = false;
        while (!quit && !shutdown.IsCancellationRequested && !game.Finished && watch.Elapsed.TotalSeconds < cli.Seconds)
        {
            if (!cli.Headless)
            {
                while (Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey(intercept: true).Key;
                    switch (key)
                    {
                        case ConsoleKey.Q: case ConsoleKey.Escape: quit = true; break;
                        case ConsoleKey.M: Select("manual"); break;
                        case ConsoleKey.O: Select("ollama"); break;
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
            if (cli.Headless && (lastHeadlessStatus != driver.Status || lastHeadlessAttempt != driver.LastAttempt))
            {
                Console.WriteLine($"tick={game.Tick} {driver.Status} {Describe(driver.LastAttempt)}");
                lastHeadlessStatus = driver.Status;
                lastHeadlessAttempt = driver.LastAttempt;
            }
            else if (!cli.Headless && watch.Elapsed.TotalMilliseconds >= nextRenderMs)
            {
                Render(game, driver);
                nextRenderMs = watch.Elapsed.TotalMilliseconds + 100;
            }
            await Task.Delay(10, shutdown.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        Console.WriteLine($"\nFINAL provider={driver.Provider} model={driver.Model} tick={game.Tick} " +
            $"score={game.Score} lives={game.Lives} accepted={driver.Accepted} rejected={driver.Rejected} " +
            $"outcome={(game.Finished ? game.Lives > 0 ? "victory" : "game-over" : "duration/quit")} status={driver.Status}");
        if (driver.Failed) return 1;
        if (cli.Headless && driver.Provider != "manual" && driver.Accepted == 0)
        {
            Console.Error.WriteLine("No AI/replay decision applied within the run; use --probe or a longer duration.");
            return 1;
        }
        return 0;

        void Select(string name)
        {
            try
            {
                var provider = providers.Get(name);
                driver.Switch(game, name, provider.Model, provider.Client);
            }
            catch (ArgumentException ex)
            {
                driver.Switch(game, name, "not configured", null, ex.Message);
                Console.Error.WriteLine($"ERROR: {ex.Message} Select M for explicit manual fallback.");
            }
        }
    }
    catch (ArgumentException ex) { Console.Error.WriteLine($"Configuration error: {ex.Message}"); return 2; }
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
        "A/Left D/Right Space S | M manual O Ollama F Foundry R OFFLINE | Q quit",
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
