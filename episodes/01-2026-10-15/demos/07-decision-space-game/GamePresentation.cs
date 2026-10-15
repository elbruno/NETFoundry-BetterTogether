/*
 * Goal: Make the native-decision game readable in a livestream terminal.
 * A. Render individual sprites with Spectre styles, keeping ASCII geometry.
 * B. Put tactical state and real SDK usage alongside the battlefield.
 * C. Present a mission report that distinguishes outcome, time, and usage coverage.
 */
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DecisionSpaceGame;

public static class GamePresentation
{
    public static IRenderable Screen(Game game, DecisionDriver driver, TimeSpan elapsed, int width, string? outcome,
        bool paused = false)
    {
        if (width < 72)
            return new Panel("Enlarge the terminal to 72 columns (110 recommended).\nM/F/O select | Q exits.")
                .BorderStyle(Color.Yellow);
        int scale = width >= 138 ? 3 : 2;
        var board = new Panel(new Markup(Battlefield(game, scale)))
            .Header("[bold cyan] BATTLE SECTOR / SEED 42 [/]")
            .Border(BoxBorder.Rounded).BorderStyle(Color.DeepSkyBlue1).Padding(1, 0);
        int boardWidth = Game.Width * scale + 4;
        board.Width = boardWidth;
        var telemetry = Status(game, driver, elapsed, outcome, paused);
        IRenderable center;
        if (width >= boardWidth + 37)
        {
            var grid = new Grid().AddColumn(new GridColumn().Width(boardWidth))
                .AddColumn(new GridColumn().Width(width - boardWidth - 2));
            grid.AddRow(board, new Panel(telemetry).Header("[bold magenta] FLIGHT TELEMETRY [/]")
                .Border(BoxBorder.Rounded).BorderStyle(Color.Magenta1).Padding(1, 0).Expand());
            center = grid;
        }
        else
        {
            // Compact terminals keep the important HUD visible above the board.
            center = new Rows(
                new Markup($"[bold]SCORE {game.Score:D4}[/]  [cyan]HULL {new string('+', game.Lives)}[/]  " +
                    $"ENEMIES {game.Enemies.Count}  WALL {elapsed.TotalSeconds:F1}s" +
                    (game.ShowingDamage ? " [bold red]HIT![/]" : "")),
                new Markup($"[yellow]{Markup.Escape(outcome ?? (paused ? "PAUSED / P resumes" : driver.Status))}[/]"),
                new Markup($"[green]{FireState(game)}[/]  ACTION: {ActionState(game)}"),
                board);
        }
        return new Rows(
            new Rule("[bold cyan] DECISION SPACE [/][grey] .NET + Foundry + Ollama [/]").RuleStyle("blue"),
            new Markup($"[bold yellow] {driver.Provider.ToUpperInvariant()} [/][grey] {Markup.Escape(driver.Model)} [/]" +
                (driver.HasPendingRequest ? " [cyan]THINKING / WORLD PAUSED[/]" : "")),
            center,
            new Markup(outcome is null ?
                paused ? "[bold yellow]PAUSED[/]  [yellow]P[/] resume  [yellow]N[/] new mission  [yellow]Q[/] quit" :
                "[yellow]M[/] manual  [yellow]F[/] Foundry  [yellow]O[/] Ollama  [yellow]P[/] pause  [yellow]N[/] new  [yellow]Q[/] quit" :
                "[bold yellow]MISSION ENDED[/]  [yellow]N[/] new mission  [yellow]Q[/] print final report"),
            new Markup("[grey]A/Left D/Right move | Space fire | S hold. AI: think, animate 4 ticks. Manual: real-time.[/]"));
    }

    public static string Battlefield(Game game, int scale)
    {
        var cells = new string[Game.Height, Game.Width];
        for (int y = 0; y < Game.Height; y++)
            for (int x = 0; x < Game.Width; x++)
                cells[y, x] = y < Game.PlayerY && (x * 7 + y * 11) % 53 == 0
                    ? $"[grey23].{new string(' ', scale - 1)}[/]" : new string(' ', scale);
        foreach (var enemy in game.Enemies)
        {
            int design = game.EnemyDesign(enemy);
            string color = design switch { 0 => "yellow", 1 => "magenta1", _ => "orange1" };
            bool alternate = game.Tick / 10 % 2 != 0;
            string sprite = (scale, design) switch
            {
                (3, 0) => alternate ? "\\V/" : "{W}",
                (3, 1) => alternate ? "<+>" : "[#]",
                (3, _) => alternate ? "/v\\" : "<o>",
                (_, 0) => alternate ? "<>" : "><",
                (_, 1) => alternate ? "{}" : "[]",
                _ => alternate ? "\\/" : "/\\"
            };
            cells[enemy.Y, enemy.X] = $"[bold {color}]" + Markup.Escape(sprite) + "[/]";
        }
        foreach (var shot in game.Shots) cells[shot.Y, shot.X] = "[bold green]" + (scale == 3 ? " | " : "| ") + "[/]";
        foreach (var shot in game.HostileShots) cells[shot.Y, shot.X] = "[bold red]" + (scale == 3 ? " ! " : "! ") + "[/]";
        cells[Game.PlayerY, game.PlayerX] = $"[bold {(game.ShowingDamage ? "red" : "cyan")}]" + Markup.Escape(scale == 3 ? "/^\\" : "/^") + "[/]";
        cells[Game.PlayerY + 1, game.PlayerX] = "[bold deepskyblue1]" + Markup.Escape(scale == 3 ? "[#]" : "[]") + "[/]";
        return string.Join("\n", Enumerable.Range(0, Game.Height).Select(y =>
            string.Concat(Enumerable.Range(0, Game.Width).Select(x => cells[y, x]))));
    }

    private static string FireState(Game game) => game.FireCooldownTicks == 0
        ? "FIRE READY" : $"COOLDOWN {game.FireCooldownTicks} ticks";

    private static string ActionState(Game game) => game.LastAppliedAction switch
    {
        GameAction.MoveLeft => "MOVE LEFT",
        GameAction.MoveRight => "MOVE RIGHT",
        GameAction.Fire => "FIRE",
        GameAction.Stop => "HOLD",
        _ => "NONE YET"
    };

    private static Table Status(Game game, DecisionDriver driver, TimeSpan elapsed, string? outcome, bool paused)
    {
        var table = new Table().NoBorder().HideHeaders().AddColumn("Metric").AddColumn("Value");
        table.AddRow("Score / invaders", $"[bold yellow]{game.Score:D4}[/] / {game.Enemies.Count}");
        table.AddRow("Hull", $"[bold cyan]{new string('+', game.Lives)}[/]" +
            (game.ShowingDamage ? " [bold red]HIT![/]" : ""));
        table.AddRow("Weapon", FireState(game));
        table.AddRow("Wall / sim seconds", $"{elapsed.TotalSeconds:F1} / {game.Tick * Game.StepMilliseconds / 1000.0:F1}");
        table.AddRow("Applied / rejected", $"{driver.Statistics.Accepted} / {driver.Statistics.Rejected}");
        table.AddRow("State", Markup.Escape(outcome ?? (paused ? "PAUSED / P resumes" :
            driver.HasPendingRequest ? "Thinking / paused" : driver.Status)));
        table.AddRow("Applied action", ActionState(game));
        if (driver.LastAttempt is { Decision: { } decision } attempt)
        {
            table.AddRow("Last action", Markup.Escape(decision.Choice));
            table.AddRow("Latency", $"{attempt.LatencyMs:F0} ms");
            table.AddRow("Probability / confidence", $"{decision.Probabilities.GetValueOrDefault(decision.Choice):P0} / {decision.Confidence:P0}");
        }
        var input = driver.Statistics.Tokens(u => u.InputTokens);
        var output = driver.Statistics.Tokens(u => u.OutputTokens);
        table.AddRow("Reported input / output", $"{Count(input.Count)} / {Count(output.Count)}");
        table.AddRow("Failures / cancelled", $"{driver.Statistics.Failed} / {driver.Statistics.Cancelled}");
        table.AddRow("Shots / hits", $"{game.ShotsFired} / {game.Score / 10}");
        return table;
    }

    public static IRenderable Summary(Game game, DecisionDriver driver, TimeSpan elapsed, string outcome)
    {
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Mission statistic").AddColumn("Observed value");
        foreach (var (metric, value) in SummaryRows(game, driver, elapsed, outcome))
            table.AddRow(Markup.Escape(metric), Markup.Escape(value));
        string color = outcome == "victory" ? "green" : outcome == "defeat" ? "red" : "yellow";
        var headline = new Panel(new Markup($"[bold {color}]{Markup.Escape(outcome.ToUpperInvariant())}[/]\n" +
            $"SCORE {game.Score}  |  WALL {elapsed.TotalSeconds:F2}s\n" +
            $"REPORTED TOKENS: IN {Count(driver.Statistics.Tokens(u => u.InputTokens).Count)} / " +
            $"OUT {Count(driver.Statistics.Tokens(u => u.OutputTokens).Count)}"))
            .Border(BoxBorder.Rounded).BorderStyle(outcome == "victory" ? Color.Green :
                outcome == "defeat" ? Color.Red : Color.Yellow);
        return new Rows(new Rule("[bold cyan] MISSION REPORT [/]").RuleStyle("blue"), headline, table,
            new Text("Compare outcomes first. A quick defeat/error is not a faster victory.\n" +
                "Same seed, decision-paced rules; concurrent runs share machine/network resources, not a benchmark.\n" +
                "Usage includes all returned decisions, including discarded ones. Missing is unknown; charges can exceed reported usage."));
    }

    public static IReadOnlyList<(string Metric, string Value)> SummaryRows(
        Game game, DecisionDriver driver, TimeSpan elapsed, string outcome)
    {
        var stats = driver.Statistics;
        string Latency(double? value) => value.HasValue ? $"{value:F1} ms" : "not available";
        string Tokens(Func<ElBruno.AI.Decisions.DecisionUsage, long?> select)
        {
            var (count, coverage) = stats.Tokens(select);
            return $"{Count(count)} ({coverage}/{stats.UsageResponses} returned decisions)";
        }
        return
        [
            ("Outcome", outcome),
            ("Provider / model", $"{stats.Providers} / {driver.Model}{(stats.Mixed ? " (mixed-mode run, do not rank providers)" : "")}"),
            ("Seed / pacing", "42 / AI decision-paced, manual real-time"),
            ("User pauses", $"{stats.UserPauses} (wall time includes pauses; do not rank paused runs)"),
            ("Wall elapsed", $"{elapsed.TotalSeconds:F2} s (excludes final cleanup; includes authentication/warm-up)"),
            ("Simulated time / ticks", $"{game.Tick * Game.StepMilliseconds / 1000.0:F2} s / {game.Tick}"),
            ("Score / kills / remaining", $"{game.Score} / {game.Score / 10} / {game.Enemies.Count}"),
            ("Hull remaining", $"{game.Lives}/3"),
            ("Shots / hit rate", $"{game.ShotsFired} / {(game.ShotsFired == 0 ? "not available" : (game.Score / 10.0 / game.ShotsFired).ToString("P1", CultureInfo.InvariantCulture))} (airborne shots count as fired)"),
            ("Requests issued / completed", $"{stats.Requests} / {stats.Completed}"),
            ("Applied / rejected / failed / cancelled", $"{stats.Accepted} / {stats.Rejected} / {stats.Failed} / {stats.Cancelled}"),
            ("Decision latency mean / p50 / p95", $"{Latency(stats.MeanLatencyMs)} / {Latency(stats.MedianLatencyMs)} / {Latency(stats.P95LatencyMs)}"),
            ("Sum request latency", $"{stats.InferenceSeconds:F2} s (client-call durations, including failures/cancellation)"),
            ("Reported input tokens", Tokens(u => u.InputTokens)),
            ("Reported output tokens", Tokens(u => u.OutputTokens)),
            ("Reported total tokens", Tokens(u => u.TotalTokens)),
            ("Reported reasoning / cached", $"{Tokens(u => u.ReasoningTokens)} / {Tokens(u => u.CachedTokens)}"),
            ("Applied action mix", stats.Actions.Count == 0 ? "none" : string.Join(", ", stats.Actions.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}")))
        ];
    }

    private static string Count(long? count) => count?.ToString("N0", CultureInfo.InvariantCulture) ?? "not reported";
}
