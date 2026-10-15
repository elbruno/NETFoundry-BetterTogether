/*
 * Goal: Verify repeat counts, real parallel scheduling, and bounded cancellation.
 * A. Replace inference with explicit controlled test doubles.
 * B. Run the same circuit-scoped coordinator used by Blazor.
 * C. Assert results/counts without cloud access or fabricated live usage.
 */
using DecisionLab.Web;
using Xunit;

namespace DecisionLab.Tests;

public sealed class ComparisonRunTests
{
    [Fact]
    public async Task StartsBothProvidersBeforeAwaitingEither()
    {
        var fake = new ControlledModels();
        using var run = new ComparisonRun(fake);
        var task = run.StartAsync([DecisionQuestion.Examples[0]], 1);
        await fake.BothStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(run.IsRunning);
        Assert.False(task.IsCompleted);
        fake.Complete();
        await task;
        Assert.False(run.IsRunning);
        Assert.Equal(2, fake.Calls);
        Assert.Equal("billing", Assert.Single(run.Results).Chat!.Label);
        Assert.Null(run.Results[0].Decision!.TotalTokens);
        Assert.Equal(12, run.Results[0].Chat!.TotalTokens);
    }

    [Fact]
    public async Task RepeatsEachSelectedQuestionAndKeepsUsagePerResult()
    {
        using var run = new ComparisonRun(new ImmediateModels());
        await run.StartAsync(DecisionQuestion.Examples.Take(2).ToArray(), 2);
        Assert.Equal(4, run.PlannedPairs);
        Assert.Equal(2, run.Repetitions);
        Assert.Equal(4, run.Results.Count);
        Assert.Equal(48, run.Results.Sum(p => p.Chat!.TotalTokens));
        Assert.All(run.Results, p => Assert.Null(p.Decision!.InputTokens));
        Assert.Equal(new[] { 1, 2, 3, 4 }, run.Results.Select(p => p.Number));
        await run.StartAsync([DecisionQuestion.Examples[0]], 1);
        Assert.Single(run.Results); // New runs intentionally reset aggregates/history.
    }

    [Fact]
    public async Task StopCancelsInFlightPairWithoutStartingAnother()
    {
        var fake = new ControlledModels();
        using var run = new ComparisonRun(fake);
        var task = run.StartAsync(DecisionQuestion.Examples, 20);
        await fake.BothStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        run.Stop();
        await task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(2, fake.Calls);
        Assert.Single(run.Results);
        Assert.Equal("cancelled", run.Results[0].Chat!.Status);
        Assert.Equal("cancelled", run.Results[0].Decision!.Status);
    }

    [Fact]
    public async Task DisconnectStopsCircuitRun()
    {
        var fake = new ControlledModels();
        using var run = new ComparisonRun(fake);
        var task = run.StartAsync([DecisionQuestion.Examples[0]], 5);
        await fake.BothStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await new RunCircuitHandler(run).OnConnectionDownAsync(null!, default);
        await task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(run.IsRunning);
        Assert.Single(run.Results);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ComparisonRun.MaxRepetitions + 1)]
    public async Task RejectsOutOfRangeRepeatsWithoutCallingModels(int repeats)
    {
        var fake = new ControlledModels();
        using var run = new ComparisonRun(fake);
        await Assert.ThrowsAsync<ArgumentException>(() => run.StartAsync([DecisionQuestion.Examples[0]], repeats));
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task RejectsEmptySelectionAndOverlappingRun()
    {
        var fake = new ControlledModels();
        using var run = new ComparisonRun(fake);
        await Assert.ThrowsAsync<ArgumentException>(() => run.StartAsync([], 1));
        var first = run.StartAsync([DecisionQuestion.Examples[0]], 1);
        await fake.BothStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.StartAsync([DecisionQuestion.Examples[0]], 1));
        run.Stop();
        await first;
    }

    [Fact]
    public async Task FailureIsSeparateFromSuccessfulPeerAndNeverCreatesUsage()
    {
        using var run = new ComparisonRun(new FailedChatModels());
        await run.StartAsync([DecisionQuestion.Examples[0]], 1);
        var pair = Assert.Single(run.Results);
        Assert.Equal("failed", pair.Chat!.Status);
        Assert.False(pair.Chat.Succeeded);
        Assert.Null(pair.Chat.TotalTokens);
        Assert.True(pair.Decision!.Succeeded);
    }

    [Fact]
    public async Task SupportsMaximumRepetitionsWithBoundedParallelismAndCompleteCharts()
    {
        var models = new CountingModels();
        using var run = new ComparisonRun(models);
        await run.StartAsync(DecisionQuestion.Examples, ComparisonRun.MaxRepetitions);

        int expected = DecisionQuestion.Examples.Count * ComparisonRun.MaxRepetitions;
        Assert.False(run.IsRunning);
        Assert.Equal(expected, run.PlannedPairs);
        Assert.Equal(expected, run.Results.Count);
        Assert.Equal(expected * 2, models.Calls);
        Assert.Equal(2, models.MaximumConcurrent);
        Assert.All(run.Results, pair =>
        {
            Assert.True(pair.Chat!.Succeeded);
            Assert.True(pair.Decision!.Succeeded);
        });
        var points = ComparisonMetrics.Series(run.Results, true, new ModelPricing(1, 2));
        Assert.Equal(expected, points.Count);
        Assert.Equal(expected * 12, points[^1].Tokens);
        Assert.Equal(expected, points[^1].Pair);
    }

    private sealed class CountingModels : IModelComparison
    {
        public int Calls { get; private set; }
        public int MaximumConcurrent { get; private set; }
        private int active;
        private readonly object gate = new();
        private TaskCompletionSource bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ModelResult> ChatAsync(DecisionQuestion q, CancellationToken token) => Call(token);
        public Task<ModelResult> DecideAsync(DecisionQuestion q, CancellationToken token) => Call(token);

        private async Task<ModelResult> Call(CancellationToken token)
        {
            Task ready;
            lock (gate)
            {
                Calls++;
                active++;
                MaximumConcurrent = Math.Max(MaximumConcurrent, active);
                ready = bothStarted.Task;
                if (active == 2) bothStarted.SetResult();
            }
            try
            {
                await ready.WaitAsync(token);
                return new ModelResult("completed", "billing", 10, 10, 2, 12);
            }
            finally
            {
                lock (gate)
                {
                    if (--active == 0)
                        bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
        }
    }

    private sealed class ImmediateModels : IModelComparison
    {
        public Task<ModelResult> ChatAsync(DecisionQuestion q, CancellationToken token) =>
            Task.FromResult(new ModelResult("completed", "billing", 10, 10, 2, 12));
        public Task<ModelResult> DecideAsync(DecisionQuestion q, CancellationToken token) =>
            Task.FromResult(new ModelResult("completed", "billing", 5));
    }

    private sealed class FailedChatModels : IModelComparison
    {
        public Task<ModelResult> ChatAsync(DecisionQuestion q, CancellationToken token) =>
            Task.FromResult(new ModelResult("failed", null, 5, Error: "Synthetic failure"));
        public Task<ModelResult> DecideAsync(DecisionQuestion q, CancellationToken token) =>
            Task.FromResult(new ModelResult("completed", "billing", 5));
    }

    private sealed class ControlledModels : IModelComparison
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource BothStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        public int Calls => calls;
        public void Complete() => release.SetResult();
        public Task<ModelResult> ChatAsync(DecisionQuestion q, CancellationToken token) => Call(true, token);
        public Task<ModelResult> DecideAsync(DecisionQuestion q, CancellationToken token) => Call(false, token);
        private async Task<ModelResult> Call(bool chat, CancellationToken token)
        {
            if (Interlocked.Increment(ref calls) == 2) BothStarted.SetResult();
            try { await release.Task.WaitAsync(token); }
            catch (OperationCanceledException)
            {
                return new ModelResult("cancelled", null, 1);
            }
            return chat ? new ModelResult("completed", "billing", 10, 10, 2, 12)
                : new ModelResult("completed", "billing", 5);
        }
    }
}
