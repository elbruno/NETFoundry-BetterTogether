/*
 * Goal: Keep the same typed result as the non-instrumented local agent.
 * Model-provided values remain separate from the deterministic suggestion.
 */
using ElBruno.AI.Decisions;

namespace SupportRouting;

public sealed record RoutingDecision(
    string Team,
    double Confidence,
    IReadOnlyDictionary<string, double> Probabilities,
    DecisionUsage? Usage,
    string Answer);
