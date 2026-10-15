using System.Globalization;
using DecisionLab.Web;
using Xunit;

namespace DecisionLab.Tests;

public sealed class ComparisonMetricsTests
{
    [Theory]
    [InlineData(67L, 1L, null, 68L, true)]
    [InlineData(67L, 0L, null, 67L, true)]
    [InlineData(0L, 0L, null, 0L, true)]
    [InlineData(10L, 2L, 15L, 15L, false)]
    [InlineData(10L, null, null, null, false)]
    [InlineData(null, 2L, null, null, false)]
    [InlineData(null, null, null, null, false)]
    public void DistinguishesReportedCalculatedAndMissingTotal(long? input, long? output,
        long? reported, long? displayed, bool calculated)
    {
        var result = new ModelResult("completed", "billing", 10, input, output, reported);
        Assert.Equal(displayed, result.DisplayTokens);
        Assert.Equal(calculated, result.HasCalculatedTotal);
        Assert.Equal(reported, result.TotalTokens);
    }

    [Fact]
    public void CostsRequireExplicitNonnegativeRatesAndBothCounters()
    {
        var result = new ModelResult("completed", "billing", 10, 1_000_000, 100_000);
        Assert.Equal(3m, new ModelPricing(2, 10).Estimate(result));
        Assert.Equal(0m, new ModelPricing(0, 0).Estimate(result));
        Assert.Null(new ModelPricing(null, 10).Estimate(result));
        Assert.Null(new ModelPricing(-1, 10).Estimate(result));
        Assert.Null(new ModelPricing(2, 10).Estimate(result with { OutputTokens = null }));
        Assert.Null(new ModelPricing(2, 10).Estimate(result with { Status = "failed" }));
    }

    [Fact]
    public void CurvesUpdateForEachPeerAndNeverTurnMissingUsageIntoZero()
    {
        var first = new ComparisonPair(1, DecisionQuestion.Examples[0])
        {
            Chat = new("completed", "billing", 100, 100, 10, 110)
        };
        var pricing = new ModelPricing(2, 10);
        Assert.Empty(ComparisonMetrics.Series([first], false, pricing));
        first.Decision = new("completed", "billing", 50, 67, 1);
        var point = Assert.Single(ComparisonMetrics.Series([first], false, pricing));
        Assert.Equal(68, point.Tokens);
        Assert.Equal(0.05, point.ElapsedSeconds);
        Assert.Equal(0.000144, point.Cost!.Value, 8);
        var second = new ComparisonPair(2, first.Question)
        {
            Decision = new("completed", "billing", 70, 60)
        };
        var series = ComparisonMetrics.Series([first, second], false, pricing);
        Assert.Equal(0.12, series[1].ElapsedSeconds, 5);
        Assert.Null(series[1].Tokens);
        Assert.Null(series[1].Cost);
        Assert.Null(ComparisonMetrics.Series([first], false, new(null, null))[0].Cost);
    }

    [Fact]
    public void FailedAndCancelledCallsCountTimeButNotUsageOrCost()
    {
        var first = new ComparisonPair(1, DecisionQuestion.Examples[0])
        {
            Decision = new("completed", "billing", 100, 0, 0)
        };
        var second = new ComparisonPair(2, first.Question) { Decision = new("failed", null, 200) };
        var third = new ComparisonPair(3, first.Question) { Decision = new("cancelled", null, 300) };
        var series = ComparisonMetrics.Series([first, second, third], false, new(0, 0));
        Assert.Equal(0, series[2].Tokens);
        Assert.Equal(0, series[2].Cost);
        Assert.Equal(0.6, series[2].ElapsedSeconds, 5);
        Assert.Equal(300, series[2].LatencyMs);
        Assert.Null(ComparisonMetrics.Series([second], false, new(0, 0))[0].Tokens);
    }

    [Fact]
    public void SvgCoordinatesAreCultureInvariantAndBounded()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            MetricPoint[] points = [new(1, 0, 0, null, 0), new(10, 100, 1, null, 100)];
            Assert.Equal("40.00,180.00 600.00,30.00",
                ComparisonMetrics.Polyline(points, p => p.Tokens, 10, 100));
            Assert.Empty(ComparisonMetrics.Polyline(points, p => p.Cost, 10, 100));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
