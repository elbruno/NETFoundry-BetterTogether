/*
 * Goal: Review one prewritten synthetic answer against supplied reference evidence.
 * A. Create the Foundry client using settings stored outside the source code.
 * B. Define the reference, question and answer, then ask Score and Assess.
 * C. Print both signals and an uncalibrated, human-review-only status.
 */
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.Configuration;

// A. Environment variables override this project's user secrets.
var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .Build();

using var client = new FoundryDecisionClient(new FoundryDecisionOptions
{
    Endpoint = new Uri(configuration["Foundry:Endpoint"]!),
    Model = configuration["Foundry:Model"]!,
    ApiKey = configuration["Foundry:ApiKey"]
});

// B. This answer already exists: neither call generates a replacement.
var reference = """
    Synthetic policy: Report duplicate subscription charges within 30 days through the support portal.
    Include both charge dates and invoice references. The billing team investigates whether a refund is due.
    Do not promise a refund or completion date. Never request a full payment-card number.
    """;
var question = "I was charged twice yesterday. How do I request a refund and when will it arrive?";
// Try: "Please contact support about the duplicate charge."
// Try: "Your refund is approved and will arrive tomorrow. Send your full payment-card number."
var answer = "Request a billing review through the support portal within 30 days. Include both charge dates and invoice references. The billing team will investigate whether a refund is due. I cannot promise a refund or completion date. Do not send your full card number.";
var prompt = $"""
    Reference policy:
    {reference}

    Customer question:
    {question}

    Proposed answer:
    {answer}
    """;
var qualityRubric = new[]
{
    "Unsupported: contradicts the reference policy or requests prohibited information",
    "Poor: substantially incomplete or misleading",
    "Mostly supported: useful guidance but missing important policy conditions",
    "Complete and supported: actionable guidance covering the review process, references and no promises"
};

var quality = await client.ScoreAsync(
    prompt, "Grade the proposed answer against the reference policy and customer question.", qualityRubric);
var support = await client.AssessAsync(
    prompt, "All factual claims and requested actions in the proposed answer are supported by the reference policy, with no unsupported refund or completion-date promise.");

// C. Both branches still require a person; these thresholds are uncalibrated.
var status = quality.Score >= 2.5 && support.Probability >= 0.85
    ? "Ready for human review"
    : "Needs revision and human review";

Console.WriteLine($"Question: {question}");
Console.WriteLine($"Proposed answer: {answer}");
Console.WriteLine($"Score | Quality: {quality.Score:F3} / 3 (weighted index, not a rounded grade)");
for (var level = 0; level < quality.LevelProbabilities.Count; level++)
    Console.WriteLine($"  Quality level {level}: {quality.LevelProbabilities[level]:P1}");
Console.WriteLine($"Assess | Probability the answer is supported: {support.Probability:P1}");
Console.WriteLine($"Uncalibrated status: {status}");
Console.WriteLine("Model signals do not prove truth or policy compliance. No answer was approved or published.");
Console.WriteLine($"Score tokens: input={quality.Usage?.InputTokens?.ToString() ?? "not reported"}, output={quality.Usage?.OutputTokens?.ToString() ?? "not reported"}, total={quality.Usage?.TotalTokens?.ToString() ?? "not reported"}");
Console.WriteLine($"Assess tokens: input={support.Usage?.InputTokens?.ToString() ?? "not reported"}, output={support.Usage?.OutputTokens?.ToString() ?? "not reported"}, total={support.Usage?.TotalTokens?.ToString() ?? "not reported"}");
