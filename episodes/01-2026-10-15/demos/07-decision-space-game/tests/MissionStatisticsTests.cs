/*
 * Goal: Make launch behavior and end-of-mission comparisons testable off cloud.
 * A. Parse only the requested start switches.
 * B. Feed known request outcomes, times and nullable usage.
 * C. Assert coverage, percentiles, reset and cancellation accounting.
 */
using ElBruno.AI.Decisions;
using Spectre.Console;
using Xunit;

namespace DecisionSpaceGame.Tests;

public sealed class MissionStatisticsTests
{
    [Fact]
    public void ParsesOnlyUnambiguousModes()
    {
        Assert.Equal(new LaunchOptions(), LaunchOptions.Parse([]));
        Assert.Equal(new LaunchOptions("foundry", true), LaunchOptions.Parse(["--foundry"]));
        Assert.Equal(new LaunchOptions("ollama", true), LaunchOptions.Parse(["--ollama"]));
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--foundry", "--ollama"]));
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--anything"]));
    }

    [Fact]
    public void PreservesZeroMissingAndReportedTotalWithCoverage()
    {
        var stats = new MissionStatistics();
        stats.Select("foundry");
        stats.StartRequest();
        stats.Complete(Attempt(10, new DecisionUsage(10, 0)), "accepted");
        stats.StartRequest();
        stats.Complete(Attempt(30), "rejected");
        stats.StartRequest();
        stats.Complete(Attempt(20, new DecisionUsage(20, 1, 25)), "accepted");
        stats.StartRequest();
        stats.Complete(new DecisionAttempt(null, "failed", 40), "failed");
        Assert.Equal((30L, 2), stats.Tokens(u => u.InputTokens));
        Assert.Equal((1L, 2), stats.Tokens(u => u.OutputTokens));
        Assert.Equal((25L, 1), stats.Tokens(u => u.TotalTokens));
        Assert.Equal((null, 0), stats.Tokens(u => u.CachedTokens));
        Assert.Equal(3, stats.UsageResponses);
        Assert.Equal(4, stats.Requests);
        Assert.Equal(4, stats.Completed);
        Assert.Equal(2, stats.Accepted);
        Assert.Equal(1, stats.Rejected);
        Assert.Equal(1, stats.Failed);
        Assert.Equal(25, stats.MeanLatencyMs);
        Assert.Equal(20, stats.MedianLatencyMs);
        Assert.Equal(40, stats.P95LatencyMs);
        Assert.Equal(0.1, stats.InferenceSeconds, 5);
        stats.Select("manual");
        Assert.True(stats.Mixed);
    }

    [Fact]
    public void EmptyAndCancelledResponseUsageAreNotInventedOrLost()
    {
        var stats = new MissionStatistics();
        Assert.Null(stats.MeanLatencyMs);
        Assert.Null(stats.MedianLatencyMs);
        Assert.Null(stats.P95LatencyMs);
        Assert.Equal((null, 0), stats.Tokens(u => u.TotalTokens));
        stats.StartRequest();
        stats.Complete(Attempt(5, new DecisionUsage(outputTokens: 0)), "cancelled");
        Assert.Equal((0L, 1), stats.Tokens(u => u.OutputTokens));
        Assert.Equal((null, 0), stats.Tokens(u => u.TotalTokens));
        Assert.Equal(1, stats.Cancelled);
        Assert.Equal(0, stats.Accepted);
        Assert.Empty(stats.Actions);
    }

    [Fact]
    public async Task DisposalCountsCancellationOnceAndSummaryEscapesModelMarkup()
    {
        var game = new Game(42);
        var driver = new DecisionDriver(TimeSpan.FromSeconds(10), 4);
        driver.Switch(game, "foundry", "[red]not-markup[/]", new DelayedClient());
        driver.Update(game);
        await driver.DisposeAsync();
        await driver.DisposeAsync();
        Assert.Equal(1, driver.Statistics.Requests);
        Assert.Equal(1, driver.Statistics.Cancelled);
        Assert.Equal(1, driver.Statistics.Completed);
        Assert.Equal(0, driver.Statistics.UsageResponses);
        var rows = GamePresentation.SummaryRows(game, driver, TimeSpan.FromSeconds(3), "quit");
        Assert.Contains(rows, row => row.Metric == "Reported total tokens" && row.Value.StartsWith("not reported"));
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = 110;
        console.Write(GamePresentation.Summary(game, driver, TimeSpan.FromSeconds(3), "quit"));
        Assert.Contains("[red]not-markup[/]", writer.ToString());
    }

    [Fact]
    public async Task AutoStartCreatesOnlySelectedProviderAndStopsOnFailure()
    {
        int foundry = 0, ollama = 0;
        int code = await GameConsole.RunAsync(
            () => { foundry++; return new FailureClient(); },
            () => { ollama++; throw new InvalidOperationException(); },
            headless: true, seconds: 2, launch: new LaunchOptions("foundry", true));
        Assert.Equal(1, code);
        Assert.Equal(1, foundry);
        Assert.Equal(0, ollama);
    }

    [Fact]
    public async Task NewMissionDoesNotAttributeAnOldPendingCallToNewStatistics()
    {
        var game = new Game(42);
        var driver = new DecisionDriver(TimeSpan.FromSeconds(10), 4);
        driver.Switch(game, "ollama", "fixture", new DelayedClient());
        driver.Update(game);
        var old = driver.Statistics;
        driver.ResetStatistics();
        driver.Switch(game, "manual", "keyboard", null);
        await driver.DisposeAsync();
        Assert.Equal(1, old.Cancelled);
        Assert.Equal(0, driver.Statistics.Requests);
        Assert.Equal("manual", driver.Statistics.Providers);
    }

    private static DecisionAttempt Attempt(double latency, DecisionUsage? usage = null) =>
        new(new ChoiceDecision("fire", new Dictionary<string, double> { ["fire"] = 1 }, null, usage), null, latency);

    private sealed class FailureClient : IDecisionClient
    {
        public Task<ChoiceDecision> ChooseAsync(string input, string instructions,
            IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default) =>
            throw new DecisionException("synthetic failure");
        public Task<ScoreDecision> ScoreAsync(string input, string instructions, IReadOnlyList<string> rubric,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssessmentDecision> AssessAsync(string input, string proposition,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class DelayedClient : IDecisionClient
    {
        public async Task<ChoiceDecision> ChooseAsync(string input, string instructions,
            IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
        public Task<ScoreDecision> ScoreAsync(string input, string instructions, IReadOnlyList<string> rubric,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssessmentDecision> AssessAsync(string input, string proposition,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
