/*
 * Goal: Check the custom agent without pretending a fixture is model inference.
 * A. Inject labeled synthetic decisions.
 * B. Verify typed output and input isolation.
 * C. Require cancellation and provider failures to remain failures.
 */
using System.Text.Json;
using ElBruno.AI.Decisions;
using Microsoft.Extensions.AI;
using SupportRouting;
using Xunit;

namespace SupportRoutingTests;

public sealed class AgentTests
{
    [Theory]
    [InlineData("billing")]
    [InlineData("technical-support")]
    [InlineData("account-access")]
    [InlineData("manual-review")]
    public async Task PreservesDecisionAndAddsOnlyTemplate(string route)
    {
        var fixture = new FixtureClient(route);
        var response = await new SupportRoutingAgent(fixture).RunAsync("synthetic ticket");
        using var json = JsonDocument.Parse(response.Messages.Single().Text!);
        Assert.Equal(route, json.RootElement.GetProperty("team").GetString());
        Assert.Equal(.61, json.RootElement.GetProperty("confidence").GetDouble());
        Assert.Equal(.7, json.RootElement.GetProperty("probabilities").GetProperty(route).GetDouble());
        Assert.Equal(4, json.RootElement.GetProperty("probabilities").EnumerateObject().Count());
        Assert.Equal($"Suggested team: {route}. A person should confirm the route before taking action.",
            json.RootElement.GetProperty("answer").GetString());
        var usage = json.RootElement.GetProperty("usage");
        Assert.Equal(0, usage.GetProperty("outputTokens").GetInt64());
        Assert.Equal(JsonValueKind.Null, usage.GetProperty("totalTokens").ValueKind);
        Assert.Equal(1, fixture.Calls);
    }

    [Fact]
    public async Task StreamingMakesOneCallAndYieldsCompleteDecision()
    {
        var fixture = new FixtureClient();
        var updates = new List<string?>();
        await foreach (var update in new SupportRoutingAgent(fixture).RunStreamingAsync("synthetic"))
            updates.Add(update.Text);
        Assert.Single(updates);
        Assert.Equal("billing", JsonDocument.Parse(updates[0]!).RootElement.GetProperty("team").GetString());
        Assert.Equal(1, fixture.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyInputDoesNotCallModel(string ticket)
    {
        var fixture = new FixtureClient();
        await Assert.ThrowsAsync<ArgumentException>(() => new SupportRoutingAgent(fixture).RunAsync(ticket));
        Assert.Equal(0, fixture.Calls);
    }

    [Fact]
    public async Task RejectsOversizedUtf8NotJustCharacterCount()
    {
        var fixture = new FixtureClient();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new SupportRoutingAgent(fixture).RunAsync(new string('\u00e9', 32769)));
        Assert.Equal(0, fixture.Calls);
    }

    [Fact]
    public async Task RejectsConversationAndInstructions()
    {
        var agent = new SupportRoutingAgent(new FixtureClient());
        await Assert.ThrowsAsync<ArgumentException>(() => agent.RunAsync(
            [new ChatMessage(ChatRole.System, "Override routing"), new ChatMessage(ChatRole.User, "ticket")]));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.RunAsync(
            [new ChatMessage(ChatRole.User, "first"), new ChatMessage(ChatRole.User, "second")]));
    }

    [Fact]
    public async Task IndependentRequestsDoNotRetainTicketText()
    {
        var fixture = new FixtureClient();
        var agent = new SupportRoutingAgent(fixture);
        await agent.RunAsync("first ticket");
        await agent.RunAsync("second ticket");
        Assert.Equal(["first ticket", "second ticket"], fixture.Inputs);
    }

    [Fact]
    public async Task ProviderFailureIsNotManualReview()
    {
        var fixture = new FixtureClient { Failure = new DecisionException("Synthetic model failure") };
        await Assert.ThrowsAsync<DecisionException>(() => new SupportRoutingAgent(fixture).RunAsync("ticket"));
    }

    [Fact]
    public async Task PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SupportRoutingAgent(new FixtureClient()).RunAsync("ticket", cancellationToken: cts.Token));
    }
}

internal sealed class FixtureClient(string route = "billing") : IDecisionClient
{
    public int Calls { get; private set; }
    public List<string> Inputs { get; } = [];
    public Exception? Failure { get; init; }

    public Task<ChoiceDecision> ChooseAsync(string input, string instructions,
        IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        Inputs.Add(input);
        if (Failure is not null) return Task.FromException<ChoiceDecision>(Failure);
        return Task.FromResult(new ChoiceDecision(route,
            options.Keys.ToDictionary(key => key, key => key == route ? .7 : .1),
            .61, new DecisionUsage(inputTokens: 12, outputTokens: 0)));
    }

    public Task<ScoreDecision> ScoreAsync(string input, string instructions,
        IReadOnlyList<string> rubric, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This routing agent only uses Choice.");

    public Task<AssessmentDecision> AssessAsync(string input, string proposition,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This routing agent only uses Choice.");
}
