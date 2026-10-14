/*
 * Goal: Verify SDK-result handling without live model calls.
 * A. Supply explicit fake chat/decision clients.
 * B. Execute the actual adapter/error/telemetry code.
 * C. Assert reported usage, missing counters, failures, and redaction.
 */
using System.Diagnostics;
using DecisionLab.Web;
using ElBruno.AI.Decisions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DecisionLab.Tests;

public sealed class ModelComparisonTests
{
    [Fact]
    public async Task PreservesChatUsageAndLeavesDecisionUsageUnknown()
    {
        using var comparison = Create();
        var chat = await comparison.ChatAsync(DecisionQuestion.Examples[0], default);
        Assert.True(chat.Succeeded);
        Assert.Equal(12, chat.InputTokens);
        Assert.Equal(0, chat.OutputTokens);
        Assert.Equal(12, chat.TotalTokens);
        var decision = await comparison.DecideAsync(DecisionQuestion.Examples[0], default);
        Assert.True(decision.Succeeded);
        Assert.Null(decision.InputTokens);
        Assert.Null(decision.OutputTokens);
        Assert.Null(decision.TotalTokens);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"team\":\"unknown\"}")]
    [InlineData("{}")]
    public async Task InvalidChatOutputIsVisibleFailureWithNoFakeUsage(string text)
    {
        using var comparison = Create(new FakeChat(text));
        var result = await comparison.ChatAsync(DecisionQuestion.Examples[0], default);
        Assert.Equal("failed", result.Status);
        Assert.NotNull(result.Error);
        Assert.Null(result.TotalTokens);
    }

    [Fact]
    public async Task IncompleteChatOutputIsNotASuccess()
    {
        using var comparison = Create(new FakeChat(finish: ChatFinishReason.Length));
        Assert.False((await comparison.ChatAsync(DecisionQuestion.Examples[0], default)).Succeeded);
    }

    [Fact]
    public async Task ProviderFailureIsSanitizedNotSuccessShaped()
    {
        using var comparison = Create(decision: new FakeDecision(fail: true));
        var result = await comparison.DecideAsync(DecisionQuestion.Examples[0], default);
        Assert.Equal("failed", result.Status);
        Assert.DoesNotContain("private.invalid", result.Error!);
        Assert.Null(result.Label);
        Assert.Null(result.TotalTokens);
    }

    [Fact]
    public async Task CallerCancellationIsReportedWithoutInventingUsage()
    {
        using var comparison = Create(new FakeChat(wait: true));
        using var cts = new CancellationTokenSource();
        var task = comparison.ChatAsync(DecisionQuestion.Examples[0], cts.Token);
        cts.Cancel();
        var result = await task;
        Assert.Equal("cancelled", result.Status);
        Assert.Null(result.TotalTokens);
    }

    [Fact]
    public void RedactsExternalAddressAndUrlButKeepsUsage()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "redaction-test",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("redaction-test");
        using var span = source.StartActivity("chat", ActivityKind.Client)!;
        span.SetTag("server.address", "private.invalid");
        span.SetTag("url.full", "https://private.invalid/secret?token=secret");
        span.SetTag("gen_ai.usage.input_tokens", 12);
        new PublicTelemetryProcessor().OnEnd(span);
        Assert.Equal("foundry", span.GetTagItem("server.address"));
        Assert.Null(span.GetTagItem("url.full"));
        Assert.Equal(12, span.GetTagItem("gen_ai.usage.input_tokens"));
    }

    private static ModelComparison Create(IChatClient? chat = null, IDecisionClient? decision = null) =>
        new(chat ?? new FakeChat(), decision ?? new FakeDecision(), "synthetic-model",
            NullLogger<ModelComparison>.Instance);

    private sealed class FakeChat(
        string text = "{\"team\":\"billing\"}", ChatFinishReason? finish = null, bool wait = false) : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (wait) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
            {
                FinishReason = finish ?? ChatFinishReason.Stop,
                Usage = new UsageDetails { InputTokenCount = 12, OutputTokenCount = 0, TotalTokenCount = 12 }
            };
        }
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class FakeDecision(bool fail = false) : IDecisionClient
    {
        public Task<ChoiceDecision> ChooseAsync(string input, string instructions,
            IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default) =>
            fail ? throw new HttpRequestException("https://private.invalid/secret")
            : Task.FromResult(new ChoiceDecision("billing", new Dictionary<string, double> { ["billing"] = 1 }));
        public Task<ScoreDecision> ScoreAsync(string input, string instructions, IReadOnlyList<string> rubric,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AssessmentDecision> AssessAsync(string input, string proposition,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
