/*
 * Goal: Demonstrate model-assisted review of prewritten synthetic answers.
 * A. Select an answer and external configuration with explicit policy thresholds.
 * B. Ask two bounded questions per answer using the community SDK.
 * C. Print both signals and a review-only outcome, keeping failures visible.
 * Inputs: --answer supported|incomplete|unsupported OR --all; --inspect; --details.
 * Output: Quality score, support probability and review status.
 * Read next: answers.v1.json, AnswerReviewer.cs, ReviewPolicy.cs.
 * Boundary: No text generation, automatic approval, publishing or real customer data.
 */
using System.Globalization;
using System.Text.Json;
using AnswerReview;
using ElBruno.AI.Decisions.Foundry;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
try
{
    // A. Default: two calls for one prewritten answer. --all permits six calls at most.
    string answerId = "supported";
    bool all = false, explicitAnswer = false, inspect = false, details = false;
    double minimumScore = 2.5, minimumSupport = 0.85;
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--help":
                Console.WriteLine("Answer review: --answer supported|incomplete|unsupported OR --all\n--minimum-score 0..3 (default 2.5) --minimum-support 0..1 (default 0.85) --inspect --details\nCommunity SDK. Two separate calls per answer; six-call cap; 120-second total deadline.");
                return 0;
            case "--answer" when i + 1 < args.Length: answerId = args[++i]; explicitAnswer = true; break;
            case "--minimum-score" when i + 1 < args.Length: minimumScore = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
            case "--minimum-support" when i + 1 < args.Length: minimumSupport = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
            case "--all": all = true; break;
            case "--inspect": inspect = true; break;
            case "--details": details = true; break;
            default: throw new ArgumentException("Unknown or incomplete argument. Use --help.");
        }
    }
    if (all && explicitAnswer) throw new ArgumentException("Choose --answer or --all.");
    // Validate policy arguments before creating a provider or acquiring credentials.
    ReviewPolicy.Decide(null, null, minimumScore, minimumSupport);
    var fixtures = AnswerReviewer.LoadFixtures();
    var selected = all ? fixtures.Answers : fixtures.Answers.Where(a => a.Id == answerId).ToArray();
    if (selected.Length == 0) throw new ArgumentException("Unknown answer ID. Use --help.");
    using var client = new FoundryDecisionClient(DemoConfiguration.Load());
    var reviewer = new AnswerReviewer(client);
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    int failures = 0, agreement = 0;
    Console.WriteLine($"Prewritten synthetic answers | score >= {minimumScore:F2}, support >= {minimumSupport:F2} (uncalibrated)");
    Console.WriteLine("Quality is a probability-weighted zero-based position [0,3]; support is a proposition probability [0,1].");
    foreach (var answer in selected)
    {
        try
        {
            if (inspect)
            {
                Console.WriteLine("Sanitized request shapes (no URL, headers, deployment or expected status):");
                Console.WriteLine(JsonSerializer.Serialize(new { model = "<deployment-name>", state = AnswerReviewer.State(fixtures, answer),
                    questions = new { decision = new { type = "score", instructions = AnswerReviewer.QualityQuestion, criteria = AnswerReviewer.Rubric } } }));
                Console.WriteLine(JsonSerializer.Serialize(new { model = "<deployment-name>", state = AnswerReviewer.State(fixtures, answer),
                    questions = new { decision = new { type = "noul", instructions = AnswerReviewer.SupportProposition } } }));
                Console.WriteLine("Response fields: answers.decision.{score,probabilities,confidence} OR {noul}.");
            }
            // B. There is no generative call; the answer text already exists in the fixture.
            var result = await reviewer.EvaluateAsync(fixtures, answer, minimumScore, minimumSupport, cancellation.Token);
            if (!result.Review.Valid) { failures++; Console.WriteLine($"{answer.Id}: {result.Review.Status} | {result.Review.Reason}"); continue; }
            if (result.Review.Status == answer.ExpectedStatus) agreement++;

            // C. Neither signal proves factual truth, policy compliance or security.
            Console.WriteLine($"{answer.Id}: quality={result.Quality.Score:F3} | P(supported)={result.Support.Probability:F3}");
            Console.WriteLine($"  {result.Review.Status}: {result.Review.Reason}");
            if (details)
                for (int level = 0; level < result.Quality.LevelProbabilities.Count; level++)
                    Console.WriteLine($"  P(quality level {level})={result.Quality.LevelProbabilities[level]:F4}");
        }
        catch (Exception error)
        {
            failures++;
            var review = ReviewPolicy.Failure();
            Console.WriteLine($"{answer.Id}: {review.Status} | {review.Reason} ({DemoErrors.Describe(error)})");
            if (cancellation.IsCancellationRequested) break;
        }
    }
    Console.WriteLine($"Calls attempted: {reviewer.Calls}/6 | Failures: {failures}");
    Console.WriteLine($"Fixture-status agreement: {agreement}/{selected.Length}. Not a benchmark. SDK 0.6.2 Score/Assess do not expose provider confidence.");
    Console.WriteLine("Model-assisted review only: no answer was approved or published.");
    return failures == 0 ? 0 : 1;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Failed ({DemoErrors.Describe(error)}). Check --help, external settings, Azure CLI permissions and request deadlines. No fallback or publishing occurred.");
    return 1;
}
