using ElBruno.AI.Decisions;
using Xunit;

namespace DecisionSpaceGame.Tests;

public sealed class DriverTests
{
    [Fact]
    public async Task InferenceDoesNotBlockGameAndAllowsOnlyOneRequest()
    {
        var game = new Game(42);
        var client = new ControlledClient();
        await using var driver = new DecisionDriver(TimeSpan.FromSeconds(5), 30);
        driver.Switch(game, "test", "fixture", client);
        driver.Update(game);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        for (int i = 0; i < 10; i++) { game.Step(); driver.Update(game); }
        Assert.Equal(10, game.Tick);
        Assert.Equal(1, client.Calls);
        Assert.True(driver.HasPendingRequest);
        client.Complete(GameTests.Decision("move_left"));
        await Settle(driver, game);
        Assert.Equal(1, driver.Accepted);
        Assert.Equal(GameAction.MoveLeft, game.ActiveAction);
    }

    [Fact]
    public async Task StaleDecisionNeverApplies()
    {
        var game = new Game(42);
        var client = new ControlledClient();
        await using var driver = new DecisionDriver(TimeSpan.FromSeconds(5), 2);
        driver.Switch(game, "test", "fixture", client);
        driver.Update(game);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        for (int i = 0; i < 3; i++) game.Step();
        client.Complete(GameTests.Decision("move_left"));
        await Settle(driver, game);
        Assert.Equal(1, driver.Rejected);
        Assert.Equal(0, driver.Accepted);
        Assert.Equal(GameAction.Stop, game.ActiveAction);
    }

    [Fact]
    public async Task IllegalDecisionNeverApplies()
    {
        var game = new Game(42);
        var client = new ControlledClient();
        await using var driver = new DecisionDriver(TimeSpan.FromSeconds(5), 30);
        driver.Switch(game, "test", "fixture", client);
        driver.Update(game);
        client.Complete(new ChoiceDecision("warp", new Dictionary<string, double> { ["warp"] = 1 }));
        await Settle(driver, game);
        Assert.Equal("Illegal decision discarded", driver.Status);
        Assert.Equal(0, driver.Accepted);
    }

    [Fact]
    public async Task ProviderSwitchCancelsAndRejectsLateResultWithoutOverlappingRequests()
    {
        var game = new Game(42);
        var oldClient = new ControlledClient();
        var newClient = new ControlledClient();
        await using var driver = new DecisionDriver(TimeSpan.FromSeconds(5), 30);
        driver.Switch(game, "old", "fixture", oldClient);
        driver.Update(game);
        await oldClient.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        driver.Switch(game, "new", "fixture", newClient);
        for (int i = 0; i < 4; i++) { game.Step(); driver.Update(game); }
        Assert.True(oldClient.Token.IsCancellationRequested);
        Assert.Equal(0, newClient.Calls);
        oldClient.Complete(GameTests.Decision("move_left"));
        await Until(() =>
        {
            driver.Update(game);
            return newClient.Started.Task.IsCompleted;
        });
        Assert.Equal(0, driver.Accepted);
        Assert.Equal(1, driver.Rejected);
        newClient.Complete(GameTests.Decision("fire"));
        await Settle(driver, game);
        Assert.Equal(1, driver.Accepted);
        Assert.Single(game.Shots);
        Assert.Equal("new", driver.Provider);
    }

    [Fact]
    public async Task ManualFallbackIsExplicitAndLateAiCannotOverrideKeyboard()
    {
        var game = new Game(42);
        var client = new ControlledClient();
        await using var driver = new DecisionDriver(TimeSpan.FromSeconds(5), 30);
        driver.Switch(game, "foundry", "fixture", client);
        driver.Update(game);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        driver.Switch(game, "manual", "keyboard", null);
        game.Apply(GameAction.MoveRight);
        client.Complete(GameTests.Decision("move_left"));
        await Until(() => { driver.Update(game); return driver.Rejected == 1; });
        game.Step();
        Assert.Equal(17, game.PlayerX);
        Assert.Equal("manual", driver.Provider);
        Assert.Equal(0, driver.Accepted);
    }

    [Fact]
    public async Task FailureStaysVisibleAndDoesNotSilentlyRetryOrFallback()
    {
        var game = new Game(42);
        var client = new ControlledClient();
        await using var driver = new DecisionDriver(TimeSpan.FromSeconds(5), 30);
        driver.Switch(game, "foundry", "fixture", client);
        driver.Update(game);
        client.Fail(new DecisionException("fixture") { StatusCode = 403 });
        await Settle(driver, game);
        for (int i = 0; i < 20; i++) { game.Step(); driver.Update(game); }
        Assert.True(driver.Failed);
        Assert.Contains("403", driver.Status);
        Assert.Equal("foundry", driver.Provider);
        Assert.Equal(1, client.Calls);
        driver.Switch(game, "manual", "keyboard", null);
        Assert.False(driver.Failed);
    }

    [Fact]
    public async Task TimeoutCancelsAndDiscardsEvenAnUncooperativeLateSuccess()
    {
        var game = new Game(42);
        var client = new ControlledClient();
        await using var driver = new DecisionDriver(TimeSpan.FromMilliseconds(30), 30);
        driver.Switch(game, "test", "fixture", client);
        driver.Update(game);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Until(() => client.Token.IsCancellationRequested);
        client.Complete(GameTests.Decision("fire"));
        await Settle(driver, game);
        Assert.True(driver.Failed);
        Assert.Equal("Request timed out.", driver.Status);
        Assert.Empty(game.Shots);
    }

    [Fact]
    public async Task CooperativeCancellationIsVisible()
    {
        var game = new Game(42);
        var client = new ControlledClient(cooperative: true);
        await using var driver = new DecisionDriver(TimeSpan.FromMilliseconds(30), 30);
        driver.Switch(game, "test", "fixture", client);
        driver.Update(game);
        await Until(() => { driver.Update(game); return driver.Failed && !driver.HasPendingRequest; });
        Assert.False(driver.HasPendingRequest);
        Assert.Contains("timed out", driver.Status);
    }

    [Fact]
    public async Task ShutdownCancelsBeforeDisposing()
    {
        var game = new Game(42);
        var client = new ControlledClient(cooperative: true);
        var driver = new DecisionDriver(TimeSpan.FromSeconds(5), 30);
        driver.Switch(game, "test", "fixture", client);
        driver.Update(game);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await driver.DisposeAsync();
        Assert.True(client.Token.IsCancellationRequested);
    }

    private static Task Settle(DecisionDriver driver, Game game) =>
        Until(() => { driver.Update(game); return driver.LastAttempt is not null; });

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition()) await Task.Delay(5, timeout.Token);
    }

    private sealed class ControlledClient(bool cooperative = false) : IDecisionClient
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<ChoiceDecision> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }
        public int Calls { get; private set; }

        public async Task<ChoiceDecision> ChooseAsync(string input, string instructions,
            IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
        {
            Calls++;
            Token = cancellationToken;
            Started.TrySetResult();
            return cooperative ? await _result.Task.WaitAsync(cancellationToken) : await _result.Task;
        }
        public void Complete(ChoiceDecision result) => _result.TrySetResult(result);
        public void Fail(Exception error) => _result.TrySetException(error);
        public Task<ScoreDecision> ScoreAsync(string input, string instructions, IReadOnlyList<string> rubric,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssessmentDecision> AssessAsync(string input, string proposition,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
