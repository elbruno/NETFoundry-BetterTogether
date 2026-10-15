/*
 * Goal: Exercise the actual rendered level, physics and controller.
 * A. Opt in to desktop tests after building Release content.
 * B. Verify pause, takeover, restart and manual physics without UI key injection.
 * C. Optionally exercise real configured providers without logging configuration.
 */
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Platformer2D.Core;
using Platformer2D.Core.Inputs;
using Platformer2D.Screens;
using Platformer2D.ScreenManagers;
using ElBruno.AI.Decisions;
using Xunit;
using Xunit.Abstractions;

namespace PlatformerDecisions.Tests;

public sealed class DesktopFactAttribute : FactAttribute
{
    public DesktopFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PLATFORMER_DESKTOP_TESTS") != "1")
            Skip = "Set PLATFORMER_DESKTOP_TESTS=1 after building Release, with a desktop available.";
    }

}

public sealed class ProviderFactAttribute : FactAttribute
{
    public ProviderFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PLATFORMER_DESKTOP_TESTS") != "1" ||
            Environment.GetEnvironmentVariable("PLATFORMER_TEST_PROVIDER") is not ("foundry" or "ollama"))
            Skip = "Opt in with PLATFORMER_DESKTOP_TESTS=1 and PLATFORMER_TEST_PROVIDER=foundry or ollama.";
    }
}

[CollectionDefinition("Desktop", DisableParallelization = true)]
public sealed class DesktopCollection;

[Collection("Desktop")]
public sealed class DesktopGameplayTests(ITestOutputHelper output)
{
    [DesktopFact]
    public void RealLevelMovesJumpsPausesAndInvalidatesOldResponses()
    {
        DecisionProviderFactory.Configure(new ConfigurationBuilder().Build());
        using var game = new Harness();
        game.Boot();
        GameplayScreen screen = game.Screen;
        var level = screen.DemoLevel;
        Assert.Equal(0, screen.Requests);
        Assert.True(level.Player.IsAlive);
        Assert.InRange(level.Player.Position.Y, 0, 720);
        var start = level.Player.Position;
        var time = level.TimeTaken;
        screen.HandleDemoKeys(new KeyboardState(Keys.P));
        for (int i = 0; i < 30; i++) game.Frame();
        Assert.Equal(start, level.Player.Position);
        Assert.Equal(time, level.TimeTaken);
        screen.HandleDemoKeys(new KeyboardState(Keys.P));

        // Apply real level physics with the same input overrides used by the controller.
        for (int i = 0; i < 12; i++)
            level.Update(new GameTime(TimeSpan.FromSeconds(i / 60d), ControlAction.StepDuration),
                new InputState(), DisplayOrientation.Default, movementOverride: 1, jumpOverride: false);
        Assert.True(level.Player.Position.X > start.X);
        float groundY = level.Player.Position.Y;
        for (int i = 0; i < 6; i++)
            level.Update(new GameTime(TimeSpan.FromSeconds((i + 12) / 60d), ControlAction.StepDuration),
                new InputState(), DisplayOrientation.Default, movementOverride: 1, jumpOverride: true);
        Assert.True(level.Player.Position.Y < groundY);

        screen.HandleDemoKeys(new KeyboardState(Keys.N));
        game.Frame();
        Assert.Equal(0, screen.Requests);
        var delayed = new DelayedClient();
        screen.RegisterClient("foundry", delayed);
        screen.HandleDemoKeys(new KeyboardState(Keys.F));
        game.Frame();
        Assert.True(screen.Pending);
        start = screen.DemoLevel.Player.Position;
        time = screen.DemoLevel.TimeTaken;
        for (int i = 0; i < 6; i++) game.Frame();
        Assert.Equal(start, screen.DemoLevel.Player.Position);
        Assert.Equal(time, screen.DemoLevel.TimeTaken);
        screen.HandleDemoKeys(new KeyboardState(Keys.M));
        delayed.Complete();
        for (int i = 0; i < 6; i++) game.Frame();
        Assert.Equal(0, screen.Accepted);
        Assert.False(screen.Pending);
        Assert.Equal(start.X, screen.DemoLevel.Player.Position.X);
        screen.HandleDemoKeys(new KeyboardState(Keys.N));
        game.Frame();
        Assert.Equal(0, screen.Requests);
        Assert.Equal(0, screen.Usage.Responses);
        output.WriteLine("Actual level: visible-height spawn, movement, jump, user/inference pause, takeover and restart verified.");
    }

    [ProviderFact]
    public void ConfiguredProviderMakesRealDecisionsAgainstActualLevel()
    {
        string? provider = Environment.GetEnvironmentVariable("PLATFORMER_TEST_PROVIDER");
        DecisionProviderFactory.Configure(new ConfigurationBuilder()
            .AddUserSecrets("netfoundry-episode01-platformer-decisions").AddEnvironmentVariables().Build());
        using var game = new Harness();
        game.Boot();
        var screen = game.Screen;
        screen.HandleDemoKeys(new KeyboardState(provider == "foundry" ? Keys.F : Keys.O));
        var watch = Stopwatch.StartNew();
        int limit = Environment.GetEnvironmentVariable("PLATFORMER_FULL_RUN") == "1" ? 180 : 20;
        int observed = 0;
        while (!screen.Failed && screen.DemoLevel.Player.IsAlive && !screen.DemoLevel.ReachedExit &&
            screen.Accepted < limit && watch.Elapsed < TimeSpan.FromSeconds(limit == 20 ? 110 : 290))
        {
            game.Frame();
            if (observed != screen.Accepted)
            {
                observed = screen.Accepted;
                output.WriteLine($"{observed}: {screen.Status}, position={screen.DemoLevel.Player.Position}, grounded={screen.DemoLevel.Player.IsOnGround}");
            }
        }
        var input = screen.Usage.Sum(u => u.InputTokens);
        var tokens = screen.Usage.Sum(u => u.OutputTokens);
        output.WriteLine($"{provider}: {screen.Accepted} decisions, input={input.Total}, output={tokens.Total}, " +
            $"alive={screen.DemoLevel.Player.IsAlive}, exit={screen.DemoLevel.ReachedExit}, " +
            $"position={screen.DemoLevel.Player.Position}, simulated={screen.DemoLevel.TimeTaken.TotalSeconds:F2}s");
        Assert.False(screen.Failed, screen.Status);
        Assert.True(screen.Accepted >= 3, screen.Status);
        Assert.True(screen.Usage.Responses >= 3);
        if (limit != 20)
        {
            Assert.True(screen.DemoLevel.Player.IsAlive, "Full provider run died before reaching the exit.");
            Assert.True(screen.DemoLevel.ReachedExit, "Full provider run did not reach the exit within its limits.");
        }
        screen.HandleDemoKeys(new KeyboardState(Keys.M));
        var drain = Stopwatch.StartNew();
        while (screen.Pending && drain.Elapsed < TimeSpan.FromSeconds(5)) game.Frame();
        Assert.False(screen.Pending);
    }

    [DesktopFact]
    public void ObservationExposesGapApproachAndActualJumpLatch()
    {
        using var game = new Harness();
        game.Boot();
        var level = game.Screen.DemoLevel;
        level.Player.Position = new Vector2(834, 608);
        level.Player.Velocity = new Vector2(180, 0);
        using (var state = JsonDocument.Parse(level.CaptureDecisionObservation()))
        {
            var gap = Assert.Single(state.RootElement.GetProperty("gapsAtFeetHeight").EnumerateArray());
            Assert.Equal(880, gap.GetProperty("startX").GetInt32());
            Assert.Equal(960, gap.GetProperty("endX").GetInt32());
            Assert.True(gap.GetProperty("withinJumpApproach").GetBoolean());
            Assert.InRange(gap.GetProperty("distanceFromPlayerRight").GetSingle(), 1, 80);
            Assert.True(state.RootElement.GetProperty("player").GetProperty("canStartJump").GetBoolean());
        }
        using (var state = JsonDocument.Parse(level.CaptureDecisionObservation()))
        {
            int enemyLeft = state.RootElement.GetProperty("nearbyEnemies")[0].GetProperty("left").GetInt32();
            level.Player.Position = new Vector2(enemyLeft - 60, 608);
            using var approach = JsonDocument.Parse(level.CaptureDecisionObservation());
            var enemy = approach.RootElement.GetProperty("nearbyEnemies")[0];
            Assert.True(enemy.GetProperty("aheadRight").GetBoolean());
            Assert.True(enemy.GetProperty("withinJumpApproach").GetBoolean());
            Assert.InRange(enemy.GetProperty("horizontalClearance").GetInt32(), 1, 120);
            level.Player.Position = new Vector2(834, 608);
        }

        var step = new GameTime(TimeSpan.Zero, ControlAction.StepDuration);
        var controls = new InputState();
        level.Update(step, controls, DisplayOrientation.Default, movementOverride: 1, jumpOverride: true);
        using (var state = JsonDocument.Parse(level.CaptureDecisionObservation()))
        {
            var player = state.RootElement.GetProperty("player");
            Assert.True(player.GetProperty("jumpHeld").GetBoolean());
            Assert.True(player.GetProperty("canExtendJump").GetBoolean());
            Assert.False(player.GetProperty("canStartJump").GetBoolean());
            Assert.Empty(state.RootElement.GetProperty("gapsAtFeetHeight").EnumerateArray());
        }
        level.Update(step, controls, DisplayOrientation.Default, movementOverride: 1, jumpOverride: false);
        Assert.False(level.Player.JumpButtonHeld);
        Assert.False(level.Player.CanExtendJump);
        level.Player.Reset(new Vector2(834, 608));
        Assert.False(level.Player.JumpButtonHeld);
    }

    [DesktopFact]
    public void ManualControlScriptCanCompleteTheTeachingLevel()
    {
        using var game = new Harness();
        game.Boot();
        var level = game.Screen.DemoLevel;
        for (int i = 0; i < 900 && level.Player.IsAlive && !level.ReachedExit; i++)
        {
            float x = level.Player.Position.X;
            bool jump = x is >= 830 and <= 1080 or >= 1200 and <= 1450;
            level.Update(new GameTime(TimeSpan.FromSeconds(i / 60d), ControlAction.StepDuration),
                new InputState(), DisplayOrientation.Default, movementOverride: 1, jumpOverride: jump);
        }
        Assert.True(level.Player.IsAlive, $"Player died at {level.Player.Position}.");
        Assert.True(level.ReachedExit, $"Did not reach exit at {level.Player.Position}.");
        output.WriteLine("Scripted manual controls completed the actual level. This is not model autoplay.");
    }

    [DesktopFact]
    public void PausedResponseWaitsForResumeAndInvalidDistributionStopsCalls()
    {
        DecisionProviderFactory.Configure(new ConfigurationBuilder().Build());
        using var game = new Harness();
        game.Boot();
        var screen = game.Screen;
        var client = new DelayedClient();
        screen.RegisterClient("foundry", client);
        screen.HandleDemoKeys(new KeyboardState(Keys.F));
        game.Frame();
        screen.HandleDemoKeys(new KeyboardState(Keys.P));
        var position = screen.DemoLevel.Player.Position;
        var time = screen.DemoLevel.TimeTaken;
        client.Complete();
        for (int i = 0; i < 6; i++) game.Frame();
        Assert.Equal(1, screen.Accepted);
        Assert.Equal(position, screen.DemoLevel.Player.Position);
        Assert.Equal(time, screen.DemoLevel.TimeTaken);
        Assert.Equal(1, screen.Requests);
        screen.HandleDemoKeys(new KeyboardState(Keys.P));
        for (int i = 0; i < 2; i++) game.Frame();
        Assert.True(screen.DemoLevel.TimeTaken > time);
        screen.HandleDemoKeys(new KeyboardState(Keys.N));
        Assert.Equal(0, screen.Accepted);
        Assert.Equal(0, screen.Usage.Responses);

        screen.HandleDemoKeys(new KeyboardState(Keys.M));
        var invalid = new DelayedClient();
        screen.RegisterClient("ollama", invalid);
        screen.HandleDemoKeys(new KeyboardState(Keys.O));
        game.Frame();
        invalid.Complete(incomplete: true);
        for (int i = 0; i < 8; i++) game.Frame();
        Assert.True(screen.Failed);
        Assert.Contains("ILLEGAL", screen.Status);
        int requests = screen.Requests;
        for (int i = 0; i < 6; i++) game.Frame();
        Assert.Equal(requests, screen.Requests);
        screen.HandleDemoKeys(new KeyboardState(Keys.M));
        Assert.False(screen.Failed);
    }

    [DesktopFact]
    public void ProviderFailureStopsRequestsWithoutInventingUsage()
    {
        DecisionProviderFactory.Configure(new ConfigurationBuilder().Build());
        using var game = new Harness();
        game.Boot();
        var screen = game.Screen;
        var client = new DelayedClient();
        screen.RegisterClient("foundry", client);
        screen.HandleDemoKeys(new KeyboardState(Keys.F));
        game.Frame();
        client.Fail();
        for (int i = 0; i < 10; i++) game.Frame();
        Assert.True(screen.Failed);
        Assert.Contains("REQUEST FAILED", screen.Status);
        Assert.Equal(1, screen.Requests);
        Assert.Equal(0, screen.Usage.Responses);
        Assert.Equal((null, 0), screen.Usage.Sum(u => u.InputTokens));
        screen.HandleDemoKeys(new KeyboardState(Keys.M));
        Assert.False(screen.Failed);
    }

    private sealed class Harness : Platformer2DGame
    {
        internal GameplayScreen Screen => Components.OfType<ScreenManager>().Single()
            .GetScreens().OfType<GameplayScreen>().Single();

        internal void Boot()
        {
            for (int i = 0; i < 110; i++) Frame();
        }

        internal void Frame()
        {
            RunOneFrame();
            Thread.Sleep(17);
        }
    }

    private sealed class DelayedClient : IDecisionClient
    {
        private readonly TaskCompletionSource<ChoiceDecision> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Fail() => completion.TrySetException(new DecisionException("Synthetic test failure."));
        internal void Complete(bool incomplete = false) => completion.TrySetResult(new ChoiceDecision("right",
            incomplete ? new Dictionary<string, double> { ["right"] = 1 } :
            ControlAction.Options.Keys.ToDictionary(k => k, k => k == "right" ? 1d : 0d)));
        public Task<ChoiceDecision> ChooseAsync(string input, string instructions,
            IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default) => completion.Task;
        public Task<ScoreDecision> ScoreAsync(string input, string instructions, IReadOnlyList<string> rubric,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssessmentDecision> AssessAsync(string input, string proposition,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
