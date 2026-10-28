/*
 * Goal: Keep the native decision distinct from a generated chat answer.
 * A. Preserve the provider's label, distribution, confidence, and optional usage.
 * B. Add only a deterministic, human-readable suggestion.
 * C. Serialize this value inside the standard Responses envelope.
 */
using ElBruno.AI.Decisions;

namespace SupportRouting;

public sealed record RoutingDecision(
    string Team,
    double Confidence,
    IReadOnlyDictionary<string, double> Probabilities,
    DecisionUsage? Usage,
    string Answer);
