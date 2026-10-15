/*
 * Goal: Exercise graphical lifecycle policy without credentials or a window.
 * A. Control asynchronous decisions and elapsed time.
 * B. Pause, resume, switch and restart with a request pending.
 * C. Verify frozen inference, cancellation drain and four-tick actions.
 */
using ElBruno.AI.Decisions;
using Xunit;

namespace DecisionSpaceGame.Tests;

public sealed class MissionTests
{
    [Fact]
    public async Task WelcomeAndManualNeverConstructProviders()
    {
        await using var mission = new Mission(() => throw new Exception("network"), () => throw new Exception("network"));
        mission.Update(10);
        Assert.True(mission.Welcome);
        Assert.Equal(0, mission.World.Tick);
        mission.Select("manual");
        mission.Update(.05, GameAction.MoveLeft);
        Assert.Equal(15, mission.World.PlayerX);
        for (int i = 0; i < 3; i++) mission.Update(.05, GameAction.Fire);
        Assert.Equal(12, mission.World.PlayerX);
        Assert.Empty(mission.World.Shots);
        mission.Update(.05, GameAction.Fire);
        Assert.Equal(1, mission.World.ShotsFired);
        Assert.Equal(0, mission.Driver.Statistics.Requests);
    }

    [Fact]
    public async Task FocusLossRequiresExplicitResumeWithoutCatchup()
    {
        await using var mission = Manual();
        mission.Select("manual");
        mission.Update(.049, GameAction.MoveRight);
        mission.LoseFocus();
        mission.Update(20, GameAction.MoveRight);
        Assert.Equal(0, mission.World.Tick);
        Assert.True(mission.FocusPaused);
        mission.TogglePause();
        mission.Update(.049, GameAction.MoveRight);
        Assert.Equal(0, mission.World.Tick);
        mission.Update(.001, GameAction.MoveRight);
        Assert.Equal(1, mission.World.Tick);
    }

    [Fact]
    public async Task CompletedInferenceWaitsWhilePausedThenLeasesExactlyFourTicks()
    {
        var client = new ControlledClient();
        await using var mission = new Mission(() => client, () => client);
        mission.Select("foundry");
        mission.Update(.05);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        mission.Update(10);
        Assert.Equal(0, mission.World.Tick);
        mission.TogglePause();
        client.Complete("move_left");
        await Task.Delay(30);
        mission.Update(10);
        Assert.Equal(0, mission.Driver.Accepted);
        mission.TogglePause();
        await Until(() => { mission.Update(0); return mission.Driver.Accepted == 1; });
        Assert.Equal(0, mission.World.Tick);
        for (int i = 0; i < 4; i++) mission.Update(.05);
        Assert.Equal(4, mission.World.Tick);
        Assert.Equal(12, mission.World.PlayerX);
        mission.Finish("test complete");
    }

    [Fact]
    public async Task RestartDrainsOldRequestBeforeNewProviderAndKeepsOldUsage()
    {
        var oldClient = new ControlledClient();
        var nextClient = new ControlledClient();
        await using var mission = new Mission(() => oldClient, () => nextClient);
        mission.Select("foundry");
        mission.Update(0);
        await oldClient.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var oldStats = mission.Driver.Statistics;
        mission.Restart();
        mission.Select("ollama");
        mission.Update(0);
        Assert.Equal(0, nextClient.Calls);
        Assert.True(oldClient.Token.IsCancellationRequested);
        oldClient.Complete("fire");
        await Until(() => { mission.Update(0); return nextClient.Calls == 1; });
        Assert.Equal(1, oldStats.Completed);
        Assert.Equal((3L, 1), oldStats.Tokens(u => u.TotalTokens));
        Assert.Equal(0, mission.World.ShotsFired);
        Assert.Equal(0, mission.Driver.Statistics.Completed);
        nextClient.Complete("stop");
        mission.Finish("quit");
    }

    [Fact]
    public async Task MissingConfigurationStopsVisiblyAndManualRecoveryIsExplicit()
    {
        await using var mission = Manual();
        mission.Select("foundry");
        mission.Update(0);
        Assert.Equal("provider failure", mission.Outcome);
        Assert.True(mission.Driver.Failed);
        Assert.Equal(0, mission.World.Tick);
        mission.Select("manual");
        mission.Update(.05);
        Assert.Null(mission.Outcome);
        Assert.False(mission.Driver.Failed);
        Assert.Equal(1, mission.World.Tick);
    }

    [Fact]
    public async Task AiWallBoundIncludesFocusPause()
    {
        var client = new ControlledClient();
        await using var mission = new Mission(() => client, () => client);
        mission.Select("foundry");
        mission.LoseFocus();
        mission.Update(300);
        Assert.Equal("AI wall-time limit", mission.Outcome);
        Assert.Equal(0, mission.World.Tick);
    }

    [Fact]
    public async Task SelectingAiKeepsSimulationBoundAfterManualTakeover()
    {
        var client = new ControlledClient();
        await using var mission = new Mission(() => client, () => client);
        mission.Select("foundry");
        mission.Select("manual");
        for (int i = 0; i < 1200; i++)
        {
            if (mission.World.Tick % 6 == 0) ClearHostileShots(mission.World);
            mission.Update(.05);
            if (mission.Outcome is not null) break;
        }
        Assert.Equal(1200, mission.World.Tick);
        Assert.Equal("AI simulation limit", mission.Outcome);
    }

    private static void ClearHostileShots(Game world)
    {
        // Isolate the limit from seeded damage; gameplay collision tests cover actual shots.
        var field = typeof(Game).GetField("_hostileShots",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        ((List<Position>)field.GetValue(world)!).Clear();
    }

    [Fact]
    public void AtlasAndLetterboxStayWithinLogicalCanvas()
    {
        for (int i = 0; i < 9; i++)
        {
            var cell = SpaceGameWindow.AtlasCell(i);
            Assert.Equal(SpaceGameWindow.AtlasCellSize, cell.Width);
            Assert.InRange(cell.Right, SpaceGameWindow.AtlasCellSize, SpaceGameWindow.AtlasCellSize * 3);
            Assert.InRange(cell.Bottom, SpaceGameWindow.AtlasCellSize, SpaceGameWindow.AtlasCellSize * 3);
        }
        Assert.Equal(new Microsoft.Xna.Framework.Rectangle(0, 150, 1600, 900), SpaceGameWindow.Letterbox(1600, 1200));
        Assert.Equal(new Microsoft.Xna.Framework.Rectangle(0, 0, 1920, 1080), SpaceGameWindow.Letterbox(1920, 1080));
    }

    private static Mission Manual() => new(() => throw new InvalidOperationException(), () => throw new InvalidOperationException());
    private static async Task Until(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!predicate()) await Task.Delay(5, timeout.Token);
    }

    private sealed class ControlledClient : IDecisionClient
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<ChoiceDecision> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<ChoiceDecision> ChooseAsync(string input, string instructions,
            IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            Calls++;
            Started.TrySetResult();
            return result.Task;
        }
        public void Complete(string label) => result.TrySetResult(new ChoiceDecision(label,
            Actions.Options.Keys.ToDictionary(k => k, k => k == label ? 1d : 0d),
            null, new DecisionUsage(totalTokens: 3)));
        public Task<ScoreDecision> ScoreAsync(string input, string instructions, IReadOnlyList<string> rubric,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssessmentDecision> AssessAsync(string input, string proposition,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
