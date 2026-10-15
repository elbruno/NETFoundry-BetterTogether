/*
 * Goal: Compare mission outcomes without confusing speed with success or price.
 * A. Record every issued/completed request and its disposition across mode changes.
 * B. Preserve usage coverage and reported zero; never synthesize token totals.
 * C. Summarize wall time, simulation time, accuracy and latency separately.
 */
using ElBruno.AI.Decisions;

namespace DecisionSpaceGame;

public sealed class MissionStatistics
{
    private readonly HashSet<string> providers = [];
    private readonly List<double> latencies = [];
    private readonly Dictionary<string, int> actions = [];
    private readonly List<DecisionUsage?> usage = [];
    public int Requests { get; private set; }
    public int Accepted { get; private set; }
    public int Rejected { get; private set; }
    public int Failed { get; private set; }
    public int Cancelled { get; private set; }
    public int UserPauses { get; private set; }
    public int Completed => latencies.Count;
    public string Providers => providers.Count == 0 ? "manual" : string.Join(" + ", providers.Order());
    public bool Mixed => providers.Count > 1;
    public IReadOnlyDictionary<string, int> Actions => actions;
    public double? MeanLatencyMs => latencies.Count == 0 ? null : latencies.Average();
    public double? MedianLatencyMs => Percentile(0.5);
    public double? P95LatencyMs => Percentile(0.95);
    public double InferenceSeconds => latencies.Sum() / 1000;
    public int UsageResponses => usage.Count;

    public void Select(string provider) => providers.Add(provider);
    public void StartRequest() => Requests++;
    public void RecordPause() => UserPauses++;
    public void Complete(DecisionAttempt attempt, string disposition)
    {
        latencies.Add(attempt.LatencyMs);
        if (attempt.Decision is { } decision) usage.Add(decision.Usage);
        switch (disposition)
        {
            case "accepted":
                Accepted++;
                string label = attempt.Decision!.Choice;
                actions[label] = actions.GetValueOrDefault(label) + 1;
                break;
            case "rejected": Rejected++; break;
            case "cancelled": Cancelled++; break;
            case "failed": Failed++; break;
            default: throw new ArgumentException("Unknown request disposition.", nameof(disposition));
        }
    }

    public (long? Count, int Coverage) Tokens(Func<DecisionUsage, long?> select)
    {
        var values = usage.OfType<DecisionUsage>().Select(select).OfType<long>().ToArray();
        return (values.Length == 0 ? null : values.Sum(), values.Length);
    }

    private double? Percentile(double fraction) => latencies.Count == 0 ? null :
        latencies.Order().ElementAt(Math.Max(0, (int)Math.Ceiling(fraction * latencies.Count) - 1));
}
