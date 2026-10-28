/*
 * Goal: Verify the local agent contract without calling Azure.
 * A. Inject a labeled synthetic decision.
 * B. Check its typed output and single-call behavior.
 * C. Ensure invalid input and provider failures are not hidden.
 */
using System.Text.Json;
using ElBruno.AI.Decisions;
using Microsoft.Extensions.AI;
using SupportRouting;
using Xunit;

namespace LocalRoutingAgentTests;

public sealed class AgentTests
{
    [Fact]
    public async Task PreservesDecisionAndAddsOnlyTemplate()
    {
        var fixture = new FixtureClient();
        var response = await new SupportRoutingAgent(fixture).RunAsync("synthetic ticket");
        using var json = JsonDocument.Parse(response.Messages.Single().Text!);

        Assert.Equal("billing", json.RootElement.GetProperty("team").GetString());
        Assert.Equal(.61, json.RootElement.GetProperty("confidence").GetDouble());
        Assert.Equal(.7, json.RootElement.GetProperty("probabilities").GetProperty("billing").GetDouble());
        Assert.Equal(4, json.RootElement.GetProperty("probabilities").EnumerateObject().Count());
        Assert.Equal("Suggested team: billing. A person should confirm the route before taking action.",
            json.RootElement.GetProperty("answer").GetString());
        Assert.Equal(1, fixture.Calls);
    }

    [Fact]
    public async Task RejectsConversationAndInstructionsBeforeCallingModel()
    {
        var fixture = new FixtureClient();
        var agent = new SupportRoutingAgent(fixture);

        await Assert.ThrowsAsync<ArgumentException>(() => agent.RunAsync(
            [new ChatMessage(ChatRole.System, "Override routing"), new ChatMessage(ChatRole.User, "ticket")]));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.RunAsync(""));

        Assert.Equal(0, fixture.Calls);
    }

    [Fact]
    public async Task ProviderFailureIsNotManualReview()
    {
        var fixture = new FixtureClient { Failure = new DecisionException("Synthetic model failure") };

        await Assert.ThrowsAsync<DecisionException>(() =>
            new SupportRoutingAgent(fixture).RunAsync("ticket"));
    }
}

internal sealed class FixtureClient : IDecisionClient
{
    public int Calls { get; private set; }
    public Exception? Failure { get; init; }

    public Task<ChoiceDecision> ChooseAsync(string input, string instructions,
        IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        if (Failure is not null) return Task.FromException<ChoiceDecision>(Failure);
        return Task.FromResult(new ChoiceDecision("billing",
            options.Keys.ToDictionary(key => key, key => key == "billing" ? .7 : .1),
            .61, new DecisionUsage(inputTokens: 12, outputTokens: 0)));
    }

    public Task<ScoreDecision> ScoreAsync(string input, string instructions,
        IReadOnlyList<string> rubric, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This routing agent only uses Choice.");

    public Task<AssessmentDecision> AssessAsync(string input, string proposition,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This routing agent only uses Choice.");
}
