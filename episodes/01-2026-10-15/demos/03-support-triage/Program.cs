/*
 * Goal: Show a compact support-triage walkthrough with application-owned policy.
 * A. Select one synthetic ticket by default and a demonstration threshold.
 * B. Execute three calls per ticket within a fixed budget and deadline.
 * C. Display simulated suggestions, failures and labeled-subset agreement.
 * Inputs: --ticket ID or --all, --threshold 0..1, --details, --inspect.
 * Output: Numeric signals and review status, with nonzero status for failures.
 * Read next: Triage.cs, then DecisionPolicy.cs.
 * Boundary: No support system is connected; tiny fixture agreement is not a benchmark.
 */
using System.Globalization;
using System.Text.Json;
using ElBruno.AI.Decisions.Foundry;
using SupportTriage;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
try
{
    // A. Default: one ticket, three calls. Opting into all fixtures permits at most 18.
    string ticketId = "billing";
    bool all = false, details = false, inspect = false, explicitTicket = false;
    double threshold = 0.75;
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--help":
                Console.WriteLine("Support triage: --ticket billing|technical|sales|account|ambiguous|adversarial OR --all\n--threshold 0..1 (default 0.75) --details --inspect\nCommunity SDK. Three separate calls per ticket; 18-call cap; 120-second total deadline.");
                return 0;
            case "--ticket" when i + 1 < args.Length: ticketId = args[++i]; explicitTicket = true; break;
            case "--threshold" when i + 1 < args.Length: threshold = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
            case "--all": all = true; break;
            case "--details": details = true; break;
            case "--inspect": inspect = true; break;
            default: throw new ArgumentException("Unknown or incomplete argument. Use --help.");
        }
    }
    if (all && explicitTicket) throw new ArgumentException("Choose --ticket or --all.");
    if (!double.IsFinite(threshold) || threshold < 0 || threshold > 1)
        throw new ArgumentException("Threshold must be finite and in [0,1].");
    var fixtures = Triage.LoadFixtures();
    var selected = all ? fixtures : fixtures.Where(t => t.Id == ticketId).ToArray();
    if (selected.Length == 0) throw new ArgumentException("Unknown ticket ID. Use --help.");
    using var client = new FoundryDecisionClient(DemoConfiguration.Load());
    var triage = new Triage(client);
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    int failures = 0, agreement = 0, labeled = selected.Count(t => t.ExpectedQueue is not null), validLabeled = 0;
    Console.WriteLine($"Synthetic tickets | threshold {threshold:F2} (uncalibrated teaching example)");
    Console.WriteLine("Urgency: probability-weighted zero-based index [0,3], not a rounded label.");
    foreach (var ticket in selected)
    {
        try
        {
            if (inspect)
            {
                Console.WriteLine("Sanitized request shapes (three requests; no URL, headers, deployment or expected labels):");
                Console.WriteLine(JsonSerializer.Serialize(new { model = "<deployment-name>", state = ticket.Text,
                    questions = new { decision = new { type = "choice", instructions = Triage.QueueQuestion, criteria = Triage.Queues } } }));
                Console.WriteLine(JsonSerializer.Serialize(new { model = "<deployment-name>", state = ticket.Text,
                    questions = new { decision = new { type = "score", instructions = Triage.UrgencyQuestion, criteria = Triage.UrgencyRubric } } }));
                Console.WriteLine(JsonSerializer.Serialize(new { model = "<deployment-name>", state = ticket.Text,
                    questions = new { decision = new { type = "noul", instructions = Triage.AccessProposition } } }));
                Console.WriteLine("Response fields: answers.decision.{choice,probabilities,confidence} OR {score,probabilities,confidence} OR {noul}.");
            }
            // B. Ticket text alone enters each question; expected labels remain local.
            var result = await triage.EvaluateAsync(ticket, threshold, cancellation.Token);
            if (!result.Review.Valid) { failures++; Console.WriteLine($"{ticket.Id}: {result.Review.Status} | {result.Review.Reason}"); continue; }
            if (ticket.ExpectedQueue is not null)
            {
                validLabeled++;
                if (ticket.ExpectedQueue == result.Queue.Choice) agreement++;
            }

            // C. This is a displayed suggestion, never automatic routing.
            Console.WriteLine($"{ticket.Id}: queue={result.Queue.Choice} | urgency={result.Urgency.Score:F3} | P(account access)={result.Access.Probability:F3}");
            Console.WriteLine($"  {result.Review.Status}: {result.Review.Reason}");
            Console.WriteLine($"  P(selected label)={result.Queue.Probabilities[result.Queue.Choice]:F3}; provider confidence={result.Queue.Confidence:F3} (different signals)");
            if (details)
            {
                foreach (var item in result.Queue.Probabilities) Console.WriteLine($"  P({item.Key})={item.Value:F4}");
                for (int level = 0; level < result.Urgency.LevelProbabilities.Count; level++)
                    Console.WriteLine($"  P(urgency level {level})={result.Urgency.LevelProbabilities[level]:F4}");
                Console.WriteLine("  SDK 0.6.2 Score/Assess do not expose provider confidence.");
            }
        }
        catch (Exception error)
        {
            failures++;
            var review = DecisionPolicy.Failure();
            Console.WriteLine($"{ticket.Id}: {review.Status} | {review.Reason} ({DemoErrors.Describe(error)})");
            if (cancellation.IsCancellationRequested) break;
        }
    }
    Console.WriteLine($"Calls attempted: {triage.Calls}/18 | Failures: {failures}");
    Console.WriteLine($"Routing agreement on labeled selected tickets: {agreement}/{labeled} (valid evaluated: {validLabeled}; failures are not silently excluded). Not a benchmark.");
    return failures == 0 ? 0 : 1;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Failed ({DemoErrors.Describe(error)}). Check --help, external settings, Azure CLI permissions and request deadlines. No fallback or routing action occurred.");
    return 1;
}
