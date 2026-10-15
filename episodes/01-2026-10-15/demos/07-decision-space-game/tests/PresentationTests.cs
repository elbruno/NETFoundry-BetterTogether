/*
 * Goal: Verify the player-facing pacing, prompt hints, and console frame.
 * A. Use deterministic state and a delayed explicit test client.
 * B. Check what advances, what is sent, and what is drawn.
 * C. Keep model quality separate from deterministic engine guarantees.
 */
using System.Text.Json;
using Spectre.Console;
using Xunit;

namespace DecisionSpaceGame.Tests;

public sealed class PresentationTests
{
    [Fact]
    public async Task ManualStartupDoesNotCreateEitherModelClient()
    {
        int calls = 0;
        ElBruno.AI.Decisions.IDecisionClient Unexpected()
        {
            calls++;
            throw new InvalidOperationException("Manual startup must not need credentials.");
        }
        Assert.Equal(0, await GameConsole.RunAsync(Unexpected, Unexpected, headless: true, seconds: 0.05));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task AiWorldPausesButManualWorldKeepsAdvancing()
    {
        var game = new Game(42);
        await using var driver = new DecisionDriver(TimeSpan.FromSeconds(3), Game.ActionTicks);
        driver.Switch(game, "manual", "keyboard", null);
        Assert.True(GameConsole.ShouldAdvance(driver, game));
        Assert.False(GameConsole.ShouldAdvance(driver, game, paused: true));
        driver.Switch(game, "ollama", "test-only", new ReplayClient());
        driver.Update(game);
        Assert.True(driver.HasPendingRequest);
        Assert.False(GameConsole.ShouldAdvance(driver, game));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (driver.Accepted == 0)
        {
            driver.Update(game);
            await Task.Delay(5, timeout.Token);
        }

        Assert.Equal(0, game.Tick);
        Assert.True(GameConsole.ShouldAdvance(driver, game));
        for (int i = 0; i < Game.ActionTicks; i++)
        {
            Assert.True(GameConsole.ShouldAdvance(driver, game));
            game.Step();
            driver.Update(game);
        }
        Assert.True(driver.HasPendingRequest);
        Assert.False(GameConsole.ShouldAdvance(driver, game));
        driver.Switch(game, "manual", "keyboard", null);
        Assert.True(GameConsole.ShouldAdvance(driver, game));
    }

    [Fact]
    public void EnemyDesignsStayDistinctAndStableDuringMovement()
    {
        var game = new Game(42);
        var designs = game.Enemies.Select(game.EnemyDesign).ToArray();
        Assert.Equal(new[] { 0, 1, 2 }, designs.Distinct().Order());
        for (int i = 0; i < 110; i++) game.Step();
        Assert.Equal(designs, game.Enemies.Select(game.EnemyDesign));
        Assert.Equal(14, game.Enemies.Count);
    }

    [Fact]
    public async Task PauseWeaponAndMissionOutcomeAreVisible()
    {
        var game = new Game(42);
        await using var driver = new DecisionDriver(TimeSpan.FromSeconds(1), 4);
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 85;
        console.Write(GamePresentation.Screen(game, driver, TimeSpan.Zero, 85, null, paused: true));
        Assert.Contains("PAUSED / P resumes", writer.ToString());
        Assert.Contains("FIRE READY", writer.ToString());
        writer.GetStringBuilder().Clear();
        game.Apply(GameAction.Fire);
        console.Write(GamePresentation.Screen(game, driver, TimeSpan.Zero, 85, null));
        Assert.Contains("COOLDOWN 6 ticks", writer.ToString());
        Assert.Contains("ACTION: FIRE", writer.ToString());
        driver.Statistics.RecordPause();
        Assert.Contains(GamePresentation.SummaryRows(game, driver, TimeSpan.Zero, "victory"),
            row => row.Metric == "User pauses" && row.Value.StartsWith("1 "));
        writer.GetStringBuilder().Clear();
        console.Write(GamePresentation.Summary(game, driver, TimeSpan.Zero, "victory"));
        Assert.Contains("VICTORY", writer.ToString());
        Assert.Contains("REPORTED TOKENS", writer.ToString());
    }

    [Fact]
    public void DamageFeedbackExpiresInSimulationTicks()
    {
        var game = new Game(42);
        while (game.LastDamageTick is null && !game.Finished) game.Step();
        Assert.NotNull(game.LastDamageTick);
        Assert.True(game.ShowingDamage);
        Assert.Contains("[bold red]/^", GamePresentation.Battlefield(game, 2));
        for (int i = 0; i < 6; i++) game.Step();
        Assert.False(game.ShowingDamage);
    }

    [Fact]
    public void SnapshotContainsPredictionAndHonestClockContract()
    {
        var game = new Game(42);
        using var state = JsonDocument.Parse(game.Snapshot());
        Assert.True(state.RootElement.GetProperty("simulationPausedDuringInference").GetBoolean());
        Assert.Equal(game.PredictFireLanes(),
            state.RootElement.GetProperty("predictedFireLanes").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Contains("PAUSED", Actions.Instructions);
        Assert.Contains("FOUR columns", Actions.Instructions);
    }

    [Fact]
    public void OnlyCurrentlyLegalActionsAreOfferedToTheModel()
    {
        var game = new Game(42);
        game.Apply(GameAction.Fire);
        Assert.DoesNotContain("fire", game.AllowedOptions().Keys);
        var options = game.AllowedOptions();
        var result = new ElBruno.AI.Decisions.ChoiceDecision("stop",
            options.Keys.ToDictionary(key => key, key => key == "stop" ? 1.0 : 0.0));
        Assert.True(DecisionDriver.IsLegal(result, game, out _, options));
        for (int i = 0; i < 40; i++) { game.Apply(GameAction.MoveLeft); game.Step(); }
        Assert.DoesNotContain("move_left", game.AllowedOptions().Keys);
        Assert.Contains("stop", game.AllowedOptions().Keys);
    }

    [Fact]
    public void PredictedLanesActuallyHitWhenFiredFromThem()
    {
        // Move first, then verify the predictor against the actual engine, not a duplicate formula.
        for (int tick = 0; tick < 30; tick++)
        {
            var game = new Game(42);
            for (int i = 0; i < tick; i++) game.Step();
            bool predictedHit = game.PredictFireLanes().Contains(game.PlayerX);
            game.Apply(GameAction.Fire);
            for (int i = 0; i < Game.PlayerY; i++) game.Step();
            Assert.Equal(predictedHit, game.Score > 0);
        }
    }

    [Theory]
    [InlineData(80, 2, "/^", "><")]
    [InlineData(140, 3, "/^\\", "{W}")]
    public async Task SpectreScreenHasSpritesColorsAndResponsiveLayout(int width, int scale, string ship, string enemy)
    {
        await using var driver = new DecisionDriver(TimeSpan.FromSeconds(1), 4);
        var game = new Game(42);
        string battlefield = GamePresentation.Battlefield(game, scale);
        var lines = battlefield.Split('\n').Select(line => Markup.Remove("x" + line + "x")[1..^1]).ToArray();
        Assert.Equal(Game.Height, lines.Length);
        Assert.All(lines, line => Assert.Equal(Game.Width * scale, line.Length));
        Assert.Contains(ship, string.Join('\n', lines));
        Assert.Contains(enemy, string.Join('\n', lines));
        Assert.Contains("[bold magenta1]", battlefield);
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = width;
        console.Write(GamePresentation.Screen(game, driver, TimeSpan.FromSeconds(1), width, null));
        Assert.Contains("DECISION SPACE", writer.ToString());
        Assert.Contains("Ollama", writer.ToString());
        Assert.All(writer.ToString().Split('\n'), line => Assert.True(line.TrimEnd('\r').Length <= width));
    }
}
