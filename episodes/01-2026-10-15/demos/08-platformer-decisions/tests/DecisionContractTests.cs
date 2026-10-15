/*
 * Goal: Verify the provider-neutral control contract without credentials.
 * A. Check the fixed action labels and jump semantics.
 * B. Reject malformed results and preserve source observations.
 * C. Keep missing usage distinct from a reported zero.
 */
using System.Text.Json;
using ElBruno.AI.Decisions;
using PlatformerDecisions;
using Xunit;

namespace PlatformerDecisions.Tests;

public sealed class DecisionContractTests
{
    [Fact]
    public void OffersSixExactMovementAndJumpControls()
    {
        Assert.Equal(["left", "right", "hold", "jump_left", "jump_right", "jump_hold"],
            ControlAction.Options.Keys);
        Assert.Equal(6, ControlAction.SimulationSteps);
        Assert.Equal(0.1, ControlAction.SimulationSteps * ControlAction.StepSeconds, 5);

        Assert.True(ControlAction.TryParse("jump_left", out var jump));
        Assert.Equal(-1, jump.Movement);
        Assert.True(jump.Jump);
        Assert.True(ControlAction.TryParse("left", out var release));
        Assert.False(release.Jump);
        Assert.False(ControlAction.TryParse("fly", out _));
    }

    [Fact]
    public void ValidatesChosenLabelAndEveryOfferedProbability()
    {
        var probabilities = ControlAction.Options.Keys.ToDictionary(key => key, _ => 1d / 6);
        Assert.True(ControlAction.IsLegal(new ChoiceDecision("right", probabilities, 0.7), out var action));
        Assert.Equal("right", action.Label);

        Assert.Throws<ArgumentException>(() => new ChoiceDecision("unknown", probabilities, 0.7));
        var incomplete = new Dictionary<string, double>(probabilities);
        incomplete.Remove("jump_hold");
        Assert.False(ControlAction.IsLegal(new ChoiceDecision("right", incomplete, 0.7), out _));

        var invalid = new Dictionary<string, double>(probabilities) { ["hold"] = 1.2 };
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChoiceDecision("right", invalid, 0.7));
    }

    [Fact]
    public void AddsPreviousControlWithoutChangingSourceSnapshot()
    {
        const string snapshot = """{"player":{"x":2}}""";
        string enriched = DecisionObservation.AddControlContext(snapshot, "jump_left", 6, true);

        using var state = JsonDocument.Parse(snapshot);
        using var request = JsonDocument.Parse(enriched);
        Assert.Equal(2, state.RootElement.GetProperty("player").GetProperty("x").GetInt32());
        Assert.Equal("jump_left", request.RootElement.GetProperty("previousControl").GetProperty("action").GetString());
        Assert.Equal(6, request.RootElement.GetProperty("previousControl").GetProperty("appliedSimulationSteps").GetInt32());
        Assert.True(request.RootElement.GetProperty("previousControl").GetProperty("jumpButtonHeld").GetBoolean());
        Assert.Equal(60, request.RootElement.GetProperty("controlContract").GetProperty("stepHz").GetInt32());

        using var initial = JsonDocument.Parse(DecisionObservation.AddControlContext(snapshot, "none", 0, false));
        Assert.Equal(0, initial.RootElement.GetProperty("previousControl")
            .GetProperty("appliedSimulationSteps").GetInt32());
    }

    [Fact]
    public void UsageLedgerPreservesUnknownAndReportedZero()
    {
        var ledger = new DecisionUsageLedger();
        ledger.Record(null);
        ledger.Record(new DecisionUsage(0, 2, null, null, 0));

        Assert.Equal(2, ledger.Responses);
        Assert.Equal(((long?)0, 1), ledger.Sum(usage => usage.InputTokens));
        Assert.Equal(((long?)2, 1), ledger.Sum(usage => usage.OutputTokens));
        Assert.Equal(((long?)null, 0), ledger.Sum(usage => usage.TotalTokens));
    }
}
