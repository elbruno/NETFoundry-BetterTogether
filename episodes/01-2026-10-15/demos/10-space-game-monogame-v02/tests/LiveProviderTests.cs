/*
 * Goal: Opt in to a small, chargeable real-provider gameplay smoke test.
 * A. Read only the new demo's external configuration.
 * B. Run at most eight real decisions and 32 simulation ticks per provider.
 * C. Report actual gameplay and nullable usage, without claiming a benchmark.
 */
using System.Diagnostics;
using ElBruno.AI.Decisions.Foundry;
using ElBruno.AI.Decisions.Ollama;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace DecisionSpaceGame.Tests;

public sealed class LiveProviderTests(ITestOutputHelper output)
{
    [LiveTheory]
    [InlineData("foundry")]
    [InlineData("ollama")]
    [Trait("Category", "LiveProvider")]
    public async Task BoundedRealProviderGameplay(string provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Mission).Assembly).AddEnvironmentVariables().Build();
        await using var mission = new Mission(
            () => new FoundryDecisionClient(new FoundryDecisionOptions
            {
                Endpoint = new Uri(configuration["Foundry:Endpoint"] ?? throw new InvalidOperationException("Configure Foundry off stream.")),
                Model = configuration["Foundry:Model"]!,
                ApiKey = configuration["Foundry:ApiKey"],
                Timeout = TimeSpan.FromSeconds(60)
            }),
            () => new OllamaDecisionClient(new OllamaDecisionOptions
            {
                Endpoint = new Uri(configuration["Ollama:Endpoint"] ?? "http://localhost:11434"),
                Model = configuration["Ollama:Model"] ?? "nimble",
                Timeout = TimeSpan.FromSeconds(60)
            }));
        mission.Select(provider);
        var watch = Stopwatch.StartNew();
        double previous = 0;
        while (mission.World.Tick < 32 && mission.Outcome is null && watch.Elapsed.TotalSeconds < 120)
        {
            double now = watch.Elapsed.TotalSeconds;
            mission.Update(now - previous);
            previous = now;
            Assert.InRange(mission.Driver.Statistics.Requests, 0, 8);
            await Task.Delay(10);
        }
        mission.Finish(mission.Outcome ?? "bounded smoke complete");
        await mission.DisposeAsync();
        var stats = mission.Driver.Statistics;
        output.WriteLine($"{provider}: outcome={mission.Outcome}, ticks={mission.World.Tick}, " +
            $"hull={mission.World.Lives}, score={mission.World.Score}, shots={mission.World.ShotsFired}, " +
            $"issued={stats.Requests}, applied={stats.Accepted}, errors={stats.Failed}, " +
            $"wall={watch.Elapsed.TotalSeconds:F1}s");
        foreach (var action in stats.Actions) output.WriteLine($"{action.Key}: {action.Value}");
        var total = stats.Tokens(u => u.TotalTokens);
        output.WriteLine($"Reported total tokens: {total.Count?.ToString() ?? "unknown"}; coverage={total.Coverage}/{stats.UsageResponses}");
        Assert.False(mission.Driver.Failed, mission.Driver.Status);
        Assert.InRange(stats.Accepted, 1, 8);
        Assert.Equal(32, mission.World.Tick);
    }
}

public sealed class LiveTheoryAttribute : TheoryAttribute
{
    public LiveTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("SPACEGAME_LIVE_TESTS") != "1")
            Skip = "Set SPACEGAME_LIVE_TESTS=1 to permit bounded, potentially chargeable real-provider calls.";
    }
}
