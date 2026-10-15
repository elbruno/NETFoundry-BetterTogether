using System.Globalization;

namespace DecisionLab.Web;

public sealed record ModelPricing(decimal? InputPerMillion, decimal? OutputPerMillion)
{
    public bool IsConfigured => InputPerMillion is >= 0 && OutputPerMillion is >= 0;

    public decimal? Estimate(ModelResult result) =>
        IsConfigured && result.Succeeded && result.InputTokens.HasValue && result.OutputTokens.HasValue
            ? (result.InputTokens.Value * InputPerMillion!.Value +
                result.OutputTokens.Value * OutputPerMillion!.Value) / 1_000_000m
            : null;
}

public sealed record MetricPoint(int Pair, double? Tokens, double ElapsedSeconds, double? Cost, double LatencyMs);

public static class ComparisonMetrics
{
    public static IReadOnlyList<MetricPoint> Series(IEnumerable<ComparisonPair> pairs,
        bool chat, ModelPricing pricing)
    {
        var points = new List<MetricPoint>();
        long tokens = 0;
        decimal cost = 0;
        double elapsed = 0;
        bool tokensComplete = true;
        bool costComplete = pricing.IsConfigured;
        bool anySuccess = false;
        foreach (var pair in pairs)
        {
            var result = chat ? pair.Chat : pair.Decision;
            if (result is null) continue;
            elapsed += result.ElapsedMs / 1000d;
            if (result.Succeeded)
            {
                anySuccess = true;
                tokensComplete &= result.DisplayTokens.HasValue;
                tokens += result.DisplayTokens ?? 0;
                var estimate = pricing.Estimate(result);
                costComplete &= estimate.HasValue;
                cost += estimate ?? 0;
            }
            points.Add(new(pair.Number, anySuccess && tokensComplete ? tokens : null, elapsed,
                anySuccess && costComplete ? (double)cost : null, result.ElapsedMs));
        }
        return points;
    }

    public static string Polyline(IEnumerable<MetricPoint> points, Func<MetricPoint, double?> selector,
        int plannedPairs, double maximum) =>
        string.Join(" ", points.Where(point => selector(point).HasValue).Select(point =>
            string.Create(CultureInfo.InvariantCulture,
                $"{40 + (point.Pair - 1) * 560d / Math.Max(1, plannedPairs - 1):F2},{180 - selector(point)!.Value * 150 / Math.Max(maximum, 0.000001):F2}")));
}
