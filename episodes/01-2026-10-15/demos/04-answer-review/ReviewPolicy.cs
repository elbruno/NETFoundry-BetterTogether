/*
 * Goal: Separate application review policy from model estimates.
 * A. Validate score distributions, assessment ranges and configured thresholds.
 * B. Require both quality and support thresholds, including equality.
 * C. Return review-only status, failing closed on invalid or missing signals.
 * Inputs: Typed model signals and uncalibrated demonstration thresholds.
 * Output: ready_for_review or needs_revision, never approval or publishing.
 * Boundary: These thresholds are not truth, compliance or security guarantees.
 */
using ElBruno.AI.Decisions;

namespace AnswerReview;

public sealed record ReviewDecision(bool Valid, string Status, string Reason);

public static class ReviewPolicy
{
    public static ReviewDecision Decide(ScoreDecision? quality, AssessmentDecision? support,
        double minimumScore = 2.5, double minimumSupport = 0.85)
    {
        // A. There are four rubric positions [0,3]; assessment is a separate [0,1] signal.
        if (!double.IsFinite(minimumScore) || minimumScore < 0 || minimumScore > 3)
            throw new ArgumentOutOfRangeException(nameof(minimumScore));
        if (!Unit(minimumSupport)) throw new ArgumentOutOfRangeException(nameof(minimumSupport));
        if (quality is null || support is null || quality.LevelProbabilities.Count != AnswerReviewer.Rubric.Count ||
            quality.LevelProbabilities.Any(p => !Unit(p)) ||
            Math.Abs(quality.LevelProbabilities.Sum() - 1) > 1e-6 ||
            !double.IsFinite(quality.Score) || quality.Score < 0 || quality.Score > 3 ||
            !Unit(support.Probability))
            return new(false, "needs_revision", "invalid model output; no approval");

        // B. A high score alone cannot compensate for low support, or vice versa.
        if (quality.Score < minimumScore || support.Probability < minimumSupport)
            return new(true, "needs_revision", "one or both demonstration thresholds failed");

        // C. Ready for a human to review is deliberately not ready to publish.
        return new(true, "ready_for_review", "both demonstration thresholds passed; human review still required");
    }

    public static ReviewDecision Failure() => new(false, "needs_revision", "request failed; no approval");
    private static bool Unit(double value) => double.IsFinite(value) && value >= 0 && value <= 1;
}
