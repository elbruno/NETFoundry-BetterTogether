/*
 * Goal: Keep routing eligibility and review decisions in application code.
 * A. Validate allowlisted choice and numeric distributions.
 * B. Apply an explicit, uncalibrated selected-label probability threshold.
 * C. Require review for invalid, failed, ambiguous or unsupported cases.
 * Inputs: Typed model signals plus application/fixture review metadata.
 * Output: A simulated review status, never a real routing operation.
 * Boundary: Provider confidence is separate and is not used as a safety guarantee.
 */
using ElBruno.AI.Decisions;

namespace SupportTriage;

public sealed record ReviewDecision(bool Valid, string Status, string Reason);

public static class DecisionPolicy
{
    public static ReviewDecision Decide(ChoiceDecision? queue, ScoreDecision? urgency,
        AssessmentDecision? access, double threshold, bool explicitReview)
    {
        if (!Unit(threshold)) throw new ArgumentOutOfRangeException(nameof(threshold));
        // A. Invalid output cannot enter even simulated routing.
        if (queue is null || urgency is null || access is null ||
            !Triage.Queues.ContainsKey(queue.Choice) ||
            queue.Probabilities.Count != Triage.Queues.Count ||
            Triage.Queues.Keys.Any(k => !queue.Probabilities.ContainsKey(k)) ||
            !Distribution(queue.Probabilities.Values) || !Unit(queue.Confidence) ||
            urgency.LevelProbabilities.Count != Triage.UrgencyRubric.Count ||
            !Distribution(urgency.LevelProbabilities) ||
            !double.IsFinite(urgency.Score) || urgency.Score < 0 || urgency.Score > 3 ||
            !Unit(access.Probability))
            return new(false, "human_review", "invalid model output; no routing suggestion accepted");

        // B. Equality passes this teaching threshold; calibrate on real data before production.
        if (queue.Probabilities[queue.Choice] < threshold)
            return new(true, "human_review", "selected-label probability below demonstration threshold");

        // C. Known ambiguity and unsupported requests override even high model probability.
        if (explicitReview || queue.Choice == "other")
            return new(true, "human_review", "ambiguous, adversarial or unsupported case");
        return new(true, "simulated_suggestion", "threshold passed; no external action");
    }

    public static ReviewDecision Failure() => new(false, "human_review", "request failed; no routing suggestion accepted");
    private static bool Unit(double value) => double.IsFinite(value) && value >= 0 && value <= 1;
    private static bool Distribution(IEnumerable<double> values) =>
        values.All(Unit) && Math.Abs(values.Sum() - 1) <= 1e-6;
}
