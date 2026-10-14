/*
 * Goal: Provide explicitly synthetic actions for offline engine tests.
 * A. Define a fixed action sequence.
 * B. Return the next scripted action after a small simulated delay.
 * C. Label replay as offline in GameConsole, never as a live model.
 */
using ElBruno.AI.Decisions;

namespace DecisionSpaceGame;

public sealed class ReplayClient : IDecisionClient
{
    private int _index;
    private static readonly string[] Script = ["fire", "move_left", "fire", "move_right", "stop", "fire"];

    public async Task<ChoiceDecision> ChooseAsync(string input, string instructions,
        IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
    {
        await Task.Delay(80, cancellationToken);
        string label = Script[_index++ % Script.Length];
        return new ChoiceDecision(label, options.Keys.ToDictionary(k => k, k => k == label ? 1.0 : 0.0), 1);
    }

    public Task<ScoreDecision> ScoreAsync(string input, string instructions, IReadOnlyList<string> rubric,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AssessmentDecision> AssessAsync(string input, string proposition,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
