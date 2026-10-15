using DecisionLab.Web;
using DecisionLab.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DecisionLab.Tests;

public sealed class LiveChartTests
{
    [Fact]
    public async Task RendersMaximumRunChartsWithoutDroppingPoints()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        int count = ComparisonRun.MaxRepetitions * DecisionQuestion.Examples.Count;
        var points = Enumerable.Range(1, count)
            .Select(i => new MetricPoint(i, i * 12, i / 10d, i * 0.00001, 100)).ToArray();
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<LiveComparisonCharts>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    ["Chat"] = points,
                    ["Decision"] = points,
                    ["PlannedPairs"] = count
                }));
            return component.ToHtmlString();
        });
        Assert.Equal(count * 4, html.Split("class=\"chat-dot\"").Length - 1);
        Assert.Equal(count * 4, html.Split("class=\"decision-dot\"").Length - 1);
        Assert.Contains("pair 400:", html);
        Assert.DoesNotContain("NaN", html);
        Assert.DoesNotContain("Infinity", html);
    }

    [Fact]
    public async Task RendersFourAccessibleChartsWithUnknownCostAndRealZero()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<LiveComparisonCharts>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    ["Chat"] = new MetricPoint[] { new(1, 0, 0, null, 0) },
                    ["Decision"] = new MetricPoint[] { new(1, 68, 0.1, null, 100) },
                    ["PlannedPairs"] = 10
                }));
            return component.ToHtmlString();
        });
        Assert.Equal(4, html.Split("role=\"img\"").Length - 1);
        Assert.Contains("68.00 tokens", html);
        Assert.Contains("Decision, pair 1: 68 tokens", html);
        Assert.Contains("0.00 tokens", html);
        Assert.Contains("Unknown values are not plotted as zero", html);
        Assert.Contains("Decision: not available", html);
        Assert.DoesNotContain("NaN", html);
        Assert.DoesNotContain("Infinity", html);
        Assert.DoesNotContain("@selector", html);
    }
}
