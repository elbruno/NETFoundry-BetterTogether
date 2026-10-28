/*
 * Goal: Prove telemetry preserves the local agent's behavior without Azure.
 * A. Verify real parent/child activities, not fabricated trace JSON.
 * B. Preserve usage, nulls, cancellation, and provider failures.
 * C. Keep ticket and response content out of exported GenAI metadata.
 */
using System.Diagnostics;
using System.Text.Json;
using ElBruno.AI.Decisions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SupportRouting;
using Xunit;

namespace LocalRoutingAgentTests;

public sealed class TelemetryTests
{
    [Fact]
    public async Task AgentAndDecisionShareATraceAndPreserveResult()
    {
        var activities = new List<Activity>();
        using var listener = Listen(activities);
        var client = new FixtureClient();
        var response = await Instrument(client).RunAsync("synthetic private ticket");
        using var result = JsonDocument.Parse(response.Messages.Single().Text!);

        Assert.Equal("billing", result.RootElement.GetProperty("team").GetString());
        Assert.Equal(.61, result.RootElement.GetProperty("confidence").GetDouble());
        Assert.Equal(4, result.RootElement.GetProperty("probabilities").EnumerateObject().Count());
        Assert.Equal(1, client.Calls);
        var agent = Assert.Single(activities, a => a.Source.Name == DecisionTelemetry.AgentSourceName);
        var decision = Assert.Single(activities, a => a.Source.Name == DecisionTelemetry.SourceName);
        Assert.Equal("invoke_agent", agent.GetTagItem("gen_ai.operation.name"));
        Assert.Equal(agent.TraceId, decision.TraceId);
        Assert.Equal(agent.SpanId, decision.ParentSpanId);
        Assert.Equal(ActivityKind.Client, decision.Kind);
        Assert.Equal("decision", decision.GetTagItem("gen_ai.operation.name"));
        Assert.Equal("test-model", decision.GetTagItem("gen_ai.request.model"));
        Assert.Equal(12L, decision.GetTagItem("gen_ai.usage.input_tokens"));
        Assert.Equal(0L, decision.GetTagItem("gen_ai.usage.output_tokens"));
        Assert.Null(decision.GetTagItem("gen_ai.usage.total_tokens"));
        Assert.Null(decision.GetTagItem("gen_ai.usage.reasoning.output_tokens"));
        Assert.All(activities, activity =>
        {
            Assert.DoesNotContain(activity.TagObjects, tag =>
                tag.Key.Contains("content") || tag.Key.Contains("url") ||
                tag.Value?.ToString()?.Contains("synthetic private ticket") == true);
            Assert.Empty(activity.Events);
        });
    }

    [Fact]
    public async Task MissingUsageIsNotReportedAsZero()
    {
        var activities = new List<Activity>();
        using var listener = Listen(activities);
        await Instrument(new FixtureClient { Usage = null }).RunAsync("ticket");
        var decision = Assert.Single(activities, a => a.Source.Name == DecisionTelemetry.SourceName);
        Assert.DoesNotContain(decision.TagObjects, tag => tag.Key.StartsWith("gen_ai.usage."));
    }

    [Fact]
    public async Task ProviderFailureRemainsAnError()
    {
        var activities = new List<Activity>();
        using var listener = Listen(activities);
        await Assert.ThrowsAsync<DecisionException>(() =>
            Instrument(new FixtureClient { Failure = new DecisionException("Synthetic failure") })
                .RunAsync("ticket"));
        var decision = Assert.Single(activities, a => a.Source.Name == DecisionTelemetry.SourceName);
        Assert.Equal(ActivityStatusCode.Error, decision.Status);
        Assert.Equal(nameof(DecisionException), decision.GetTagItem("error.type"));
        Assert.Null(decision.StatusDescription);
    }

    [Fact]
    public async Task InvalidInputDoesNotCallModel()
    {
        var client = new FixtureClient();
        var agent = Instrument(client);
        await Assert.ThrowsAsync<ArgumentException>(() => agent.RunAsync(
            [new ChatMessage(ChatRole.System, "instructions"), new ChatMessage(ChatRole.User, "ticket")]));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.RunAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.RunAsync(new string('x', 64 * 1024 + 1)));
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task CancellationIsNotACompletedDecision()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Instrument(new FixtureClient()).RunAsync("ticket", cancellationToken: cancellation.Token));
    }

    private static OpenTelemetryAgent Instrument(IDecisionClient client) =>
        new(new SupportRoutingAgent(client, "test-model"), DecisionTelemetry.AgentSourceName)
        {
            EnableSensitiveData = false
        };

    private static ActivityListener Listen(List<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is DecisionTelemetry.SourceName or DecisionTelemetry.AgentSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}

internal sealed class FixtureClient : IDecisionClient
{
    public int Calls { get; private set; }
    public Exception? Failure { get; init; }
    public DecisionUsage? Usage { get; init; } = new(inputTokens: 12, outputTokens: 0);

    public Task<ChoiceDecision> ChooseAsync(string input, string instructions,
        IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        if (Failure is not null) return Task.FromException<ChoiceDecision>(Failure);
        return Task.FromResult(new ChoiceDecision("billing",
            options.Keys.ToDictionary(key => key, key => key == "billing" ? .7 : .1), .61, Usage));
    }

    public Task<ScoreDecision> ScoreAsync(string input, string instructions,
        IReadOnlyList<string> rubric, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This demo only uses Choice.");

    public Task<AssessmentDecision> AssessAsync(string input, string proposition,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This demo only uses Choice.");
}
