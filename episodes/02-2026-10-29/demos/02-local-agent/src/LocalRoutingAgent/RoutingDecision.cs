/*
 * Goal: Define the typed support-routing result returned by the local agent.
 * Keep model-provided values separate from the deterministic suggestion text.
 */
using ElBruno.AI.Decisions;

namespace SupportRouting;

public sealed record RoutingDecision(
    string Team,
    double Confidence,
    IReadOnlyDictionary<string, double> Probabilities,
    DecisionUsage? Usage,
    string Answer);
