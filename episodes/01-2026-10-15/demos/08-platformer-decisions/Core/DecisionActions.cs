/*
 * Goal: Define the model's bounded controls independently of platformer physics.
 * A. Offer only six fixed movement and jump combinations.
 * B. Treat jump as a held control for six 60 Hz simulation steps.
 * C. Validate every result against the exact supplied option set.
 */
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ElBruno.AI.Decisions;

namespace PlatformerDecisions;

public readonly record struct ControlAction(string Label, float Movement, bool Jump)
{
    public const int SimulationSteps = 6;
    public const double StepSeconds = 1d / 60d;
    public static readonly TimeSpan StepDuration = TimeSpan.FromSeconds(StepSeconds);

    public static readonly IReadOnlyDictionary<string, string?> Options =
        new Dictionary<string, string?>
        {
            ["left"] = "Hold left; release jump for six fixed 60 Hz simulation steps (100 ms).",
            ["right"] = "Hold right; release jump for six fixed 60 Hz simulation steps (100 ms).",
            ["hold"] = "Release horizontal movement and jump for six fixed simulation steps.",
            ["jump_left"] = "Hold left and hold jump for six fixed simulation steps.",
            ["jump_right"] = "Hold right and hold jump for six fixed simulation steps.",
            ["jump_hold"] = "Release horizontal movement and hold jump for six fixed simulation steps."
        };

    public static bool TryParse(string? label, out ControlAction action)
    {
        action = label switch
        {
            "left" => new(label, -1, false),
            "right" => new(label, 1, false),
            "hold" => new(label, 0, false),
            "jump_left" => new(label, -1, true),
            "jump_right" => new(label, 1, true),
            "jump_hold" => new(label, 0, true),
            _ => default
        };
        return Options.ContainsKey(label ?? string.Empty);
    }

    public static string Instructions => """
        Choose exactly one supplied action. The input is JSON game state observed by the application, not an image.
        Goal: reach the level exit alive. Progress toward the exit; avoid falling into gaps and touching live enemies. Gems are optional rewards, not a reason to stop progressing.
        Coordinates and velocity use world pixels; x increases right and y increases down. The simulation runs at 60 Hz.
        Each action controls horizontal movement and the jump button for exactly six simulation steps (100 ms); wall-clock inference time does not advance physics, enemies, timers, or simulation animations.
        A jump_* label holds jump for all six steps, including while airborne, which can extend the upstream variable-height jump. A non-jump label releases jump; a new jump can start only when the original game physics permits it. No label overrides collision or physics.
        Prioritize survival before forward progress. gapsAtFeetHeight explicitly lists missing support, not solid platforms.
        When canStartJump=true and a gap ahead has withinJumpApproach=true, choose jump_right NOW to cross it. Do not wait or stop: there is no air jump after leaving the ground.
        When canStartJump=true and an enemy aheadRight=true has withinJumpApproach=true, choose jump_right NOW to clear it before collision. Enemy distances use collision boxes, not centers.
        While canExtendJump=true choose jump_right to keep horizontal momentum and hold jump long enough to clear the hazard. Do not choose jump_hold over a gap: it stops horizontal movement.
        Once descending (velocityY>=0 and grounded=false), choose right to release jump and continue crossing. If grounded but jumpHeld=true, choose right to release before a new jump.
        Otherwise choose right toward an exit to the right, or left toward an exit to the left. Do not choose hold on safe ground: standing still does not make progress. Gems are optional.
        Use nearby collision tiles, gap edges, platforms, enemies, gems, exit direction and player velocity to choose a safe action. Return one supplied label only.
        """;

    public static bool IsLegal(ChoiceDecision decision, out ControlAction action)
    {
        action = default;
        return TryParse(decision.Choice, out action) &&
            decision.Probabilities.Count == Options.Count &&
            Options.Keys.All(decision.Probabilities.ContainsKey) &&
            decision.Probabilities.Values.All(value => double.IsFinite(value) && value is >= 0 and <= 1) &&
            Math.Abs(decision.Probabilities.Values.Sum() - 1) <= 0.01;
    }
}

public static class DecisionObservation
{
    public static string AddControlContext(string levelSnapshot, string previousAction, int previousSteps, bool jumpHeld)
    {
        using var document = JsonDocument.Parse(levelSnapshot);
        return JsonSerializer.Serialize(new
        {
            state = document.RootElement,
            previousControl = new { action = previousAction, appliedSimulationSteps = previousSteps, jumpButtonHeld = jumpHeld },
            controlContract = new { coordinateUnit = "world pixels", stepHz = 60, actionSteps = ControlAction.SimulationSteps }
        });
    }
}

public sealed class DecisionUsageLedger
{
    private readonly List<DecisionUsage> reported = new();

    public int Responses { get; private set; }

    public void Record(DecisionUsage? usage)
    {
        Responses++;
        if (usage != null) reported.Add(usage);
    }

    public (long? Total, int Coverage) Sum(Func<DecisionUsage, long?> select)
    {
        long[] values = reported.Select(select).Where(value => value.HasValue)
            .Select(value => value!.Value).ToArray();
        return (values.Length == 0 ? null : values.Sum(), values.Length);
    }

    public void Clear()
    {
        reported.Clear();
        Responses = 0;
    }
}
