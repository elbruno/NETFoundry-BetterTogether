/*
 * Goal: Verify deterministic game rules independently of a live model.
 * A. Create seeded state and bounded actions.
 * B. Apply moves and advance fixed simulation steps.
 * C. Assert keyboard mapping, snapshots, collisions, and game outcomes.
 */
using System.Text.Json;
using ElBruno.AI.Decisions;
using Xunit;

namespace DecisionSpaceGame.Tests;

public sealed class GameTests
{
    [Theory]
    [InlineData(ConsoleKey.LeftArrow, GameAction.MoveLeft)]
    [InlineData(ConsoleKey.A, GameAction.MoveLeft)]
    [InlineData(ConsoleKey.RightArrow, GameAction.MoveRight)]
    [InlineData(ConsoleKey.D, GameAction.MoveRight)]
    [InlineData(ConsoleKey.Spacebar, GameAction.Fire)]
    [InlineData(ConsoleKey.S, GameAction.Stop)]
    public void ManualKeyboardUsesSameBoundedActions(ConsoleKey key, GameAction expected)
    {
        Assert.True(Actions.TryKey(key, out var action));
        Assert.Equal(expected, action);
        Assert.False(Actions.TryKey(ConsoleKey.P, out _));
    }

    [Theory]
    [InlineData("move_left", GameAction.MoveLeft)]
    [InlineData("move_right", GameAction.MoveRight)]
    [InlineData("fire", GameAction.Fire)]
    [InlineData("stop", GameAction.Stop)]
    public void MapsExactLabels(string label, GameAction expected)
    {
        Assert.True(Actions.TryMap(label, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Fire")]
    [InlineData("move_up")]
    [InlineData(" fire ")]
    public void RejectsUnknownLabels(string label) => Assert.False(Actions.TryMap(label, out _));

    [Fact]
    public void MovementLeaseExpiresAndStops()
    {
        var game = new Game(42);
        game.Apply(GameAction.MoveLeft);
        for (int i = 0; i < 10; i++) game.Step();
        Assert.Equal(12, game.PlayerX);
        Assert.Equal(GameAction.Stop, game.ActiveAction);
    }

    [Theory]
    [InlineData(GameAction.MoveLeft, 0)]
    [InlineData(GameAction.MoveRight, Game.Width - 1)]
    public void MovementClampsToBoard(GameAction action, int expected)
    {
        var game = new Game(42);
        for (int i = 0; i < 40; i++) { game.Apply(action); game.Step(); }
        Assert.Equal(expected, game.PlayerX);
        Assert.False(game.Apply((GameAction)99));
        Assert.Equal(expected, game.PlayerX);
    }

    [Fact]
    public void FireIsSingleShotAndCooldownEnforced()
    {
        var game = new Game(42);
        game.Apply(GameAction.Fire);
        game.Apply(GameAction.Fire);
        Assert.Single(game.Shots);
        Assert.Equal(new Position(16, 17), game.Shots[0]);
        for (int i = 0; i < 6; i++) game.Step();
        Assert.Equal(0, game.FireCooldownTicks);
        game.Apply(GameAction.Fire);
        Assert.Equal(2, game.Shots.Count);
    }

    [Fact]
    public void ShotDestroysEnemyAndScores()
    {
        var game = new Game(42);
        game.Apply(GameAction.MoveLeft);
        for (int i = 0; i < 2; i++) game.Step();
        game.Apply(GameAction.Fire);
        for (int i = 0; i < 16; i++) game.Step();
        Assert.Equal(10, game.Score);
        Assert.Equal(13, game.Enemies.Count);
    }

    [Fact]
    public void SameSeedAndActionsGiveIdenticalSnapshots()
    {
        var first = new Game(123);
        var second = new Game(123);
        for (int i = 0; i < 300; i++)
        {
            var action = (GameAction)(i % 4);
            first.Apply(action); second.Apply(action);
            first.Step(); second.Step();
            Assert.Equal(first.Snapshot(), second.Snapshot());
        }
    }

    [Fact]
    public void HostileShotsAreSeededAndDamageTheShip()
    {
        var game = new Game(42);
        for (int i = 0; i < 1200 && !game.Finished; i++) game.Step();
        Assert.True(game.Lives < 3);
        Assert.All(game.HostileShots, p => Assert.InRange(p.Y, 0, Game.PlayerY - 1));
    }

    [Fact]
    public void FixedProbeSnapshotHasExplicitCoordinateAndTimingFields()
    {
        using var state = JsonDocument.Parse(new Game(42).Snapshot());
        Assert.Equal(0, state.RootElement.GetProperty("tick").GetInt32());
        Assert.Equal(50, state.RootElement.GetProperty("tickDurationMs").GetInt32());
        Assert.Equal(4, state.RootElement.GetProperty("actionDurationTicks").GetInt32());
        Assert.Equal(14, state.RootElement.GetProperty("enemies").GetArrayLength());
        Assert.Equal(18, state.RootElement.GetProperty("player").GetProperty("y").GetInt32());
    }

    [Fact]
    public void RejectsIllegalBoundsCooldownAndDistribution()
    {
        var game = new Game(42);
        for (int i = 0; i < 16; i++) { game.Apply(GameAction.MoveLeft); game.Step(); }
        Assert.False(DecisionDriver.IsLegal(Decision("move_left"), game, out _));
        game.Apply(GameAction.Fire);
        Assert.False(DecisionDriver.IsLegal(Decision("fire"), game, out _));
        Assert.False(DecisionDriver.IsLegal(new ChoiceDecision("warp",
            new Dictionary<string, double> { ["warp"] = 1 }), game, out _));
        Assert.False(DecisionDriver.IsLegal(new ChoiceDecision("stop",
            new Dictionary<string, double> { ["stop"] = 1 }), game, out _));
        Assert.True(DecisionDriver.IsLegal(Decision("stop"), game, out _));
    }

    internal static ChoiceDecision Decision(string label) =>
        new(label, Actions.Options.Keys.ToDictionary(k => k, k => k == label ? 1.0 : 0.0), 0.6);
}
