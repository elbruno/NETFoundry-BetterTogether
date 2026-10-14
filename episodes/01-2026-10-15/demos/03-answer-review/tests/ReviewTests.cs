/*
 * Goal: Validate answer-review policy and native question handling offline.
 * A. Arrange synthetic reference evidence and fake numerical model outputs.
 * B. Run policy and SDK-backed review using an in-memory HTTP handler.
 * C. Assert threshold boundaries, label isolation, call limits and failures.
 * Inputs: Synthetic fixtures only. Output: Deterministic automated tests.
 * Boundary: Tests do not prove model truthfulness or authorize publishing.
 */
using System.Net;
using System.Text.Json;
using ElBruno.AI.Decisions;
using ElBruno.AI.Decisions.Foundry;
using Xunit;

namespace AnswerReview.Tests;

public sealed class ReviewTests
{
    // A. The weighted score can lie between rubric levels.
    private static ScoreDecision Quality(double score) => new([0, 0, 3 - score, score - 2]);

    [Theory]
    [InlineData(2.499, 0.85, "needs_revision")]
    [InlineData(2.5, 0.849, "needs_revision")]
    [InlineData(2.5, 0.85, "ready_for_review")]
    [InlineData(3, 1, "ready_for_review")]
    public void BothSignalsMustMeetThresholds(double score, double support, string expected)
    {
        // B. A support estimate alone cannot compensate for an incomplete answer.
        var result = ReviewPolicy.Decide(Quality(score), new(support));
        // C. Passing means human review, never approval.
        Assert.True(result.Valid);
        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 1)]
    public void ThresholdEndpointsAreValid(double score, double support) =>
        Assert.True(ReviewPolicy.Decide(Quality(3), new(1), score, support).Valid);

    [Theory]
    [InlineData(-1, 0.85)]
    [InlineData(3.1, 0.85)]
    [InlineData(double.NaN, 0.85)]
    [InlineData(2.5, -0.1)]
    [InlineData(2.5, 1.1)]
    [InlineData(2.5, double.PositiveInfinity)]
    public void InvalidThresholdsAreRejected(double score, double support) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ReviewPolicy.Decide(Quality(3), new(1), score, support));

    [Fact]
    public void InvalidOrMissingSignalsAndFailuresNeverApprove()
    {
        Assert.False(ReviewPolicy.Decide(null, new(1)).Valid);
        Assert.False(ReviewPolicy.Decide(Quality(3), null).Valid);
        var result = ReviewPolicy.Decide(new ScoreDecision([0.5, 0.5]), new(1));
        Assert.False(result.Valid);
        Assert.Equal("needs_revision", result.Status);
        Assert.False(ReviewPolicy.Failure().Valid);
        Assert.Equal("needs_revision", ReviewPolicy.Failure().Status);
    }

    [Fact]
    public async Task SyntheticHttpTestsBothSdkQuestionTypesAndKeepsExpectedStatusOutOfState()
    {
        var handler = new NativeStub();
        using var http = new HttpClient(handler);
        using var client = new FoundryDecisionClient(http, Options());
        var reviewer = new AnswerReviewer(client);
        var set = AnswerReviewer.LoadFixtures();
        var result = await reviewer.EvaluateAsync(set, set.Answers[0], 2.5, 0.85, default);
        Assert.Equal(new[] { "score", "noul" }, handler.Types);
        Assert.Equal(2, reviewer.Calls);
        Assert.Equal(2.7, result.Quality.Score, 8);
        Assert.Equal(0.9, result.Support.Probability);
        Assert.Equal("ready_for_review", result.Review.Status);
    }

    [Theory]
    [InlineData("""{"type":"score","score":3,"probabilities":{"0":0,"1":0,"2":0.3,"3":0.7},"confidence":0.5}""")]
    [InlineData("""{"type":"score","score":3,"probabilities":{"0":0,"1":0,"2":0,"3":1},"confidence":2}""")]
    public async Task MalformedNativeScoreNeverBecomesReviewReady(string answer)
    {
        using var http = new HttpClient(new BadResponse(answer));
        using var client = new FoundryDecisionClient(http, Options());
        var reviewer = new AnswerReviewer(client);
        var set = AnswerReviewer.LoadFixtures();
        Assert.NotNull(await Record.ExceptionAsync(() => reviewer.EvaluateAsync(set, set.Answers[0], 2.5, 0.85, default)));
        Assert.Equal(1, reviewer.Calls);
    }

    [Fact]
    public async Task FakeOutputsTestCallCapAndExplicitFailure()
    {
        var fake = new FakeClient();
        var reviewer = new AnswerReviewer(fake, maxCalls: 2);
        var set = AnswerReviewer.LoadFixtures();
        Assert.Equal("needs_revision", (await reviewer.EvaluateAsync(set, set.Answers[1], 2.5, 0.85, default)).Review.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reviewer.EvaluateAsync(set, set.Answers[0], 2.5, 0.85, default));
        Assert.Equal(2, reviewer.Calls);
        var failing = new AnswerReviewer(new FakeClient { Fail = true });
        await Assert.ThrowsAsync<HttpRequestException>(() => failing.EvaluateAsync(set, set.Answers[0], 2.5, 0.85, default));
        Assert.Equal(1, failing.Calls);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => failing.EvaluateAsync(set, set.Answers[0], 2.5, 0.85, cancellation.Token));
        Assert.Equal(1, failing.Calls);
    }

    [Fact]
    public void ThreePrewrittenExamplesHaveLocalExpectationsOnly()
    {
        var set = AnswerReviewer.LoadFixtures();
        Assert.Equal(new[] { "supported", "incomplete", "unsupported" }, set.Answers.Select(a => a.Id));
        foreach (var answer in set.Answers)
        {
            using var state = JsonDocument.Parse(AnswerReviewer.State(set, answer));
            Assert.Equal(3, state.RootElement.EnumerateObject().Count());
            Assert.Equal(answer.Text, state.RootElement.GetProperty("proposedAnswer").GetString());
            Assert.False(state.RootElement.TryGetProperty("expectedStatus", out _));
        }
    }

    [Fact]
    public void SanitizedErrorsDoNotLeakRawIdentityOrHttpDiagnostics()
    {
        Assert.Equal("Foundry HTTP 401", DemoErrors.Describe(new DecisionException("sensitive response body") { StatusCode = 401 }));
        Assert.Equal("Azure CLI authentication failed", DemoErrors.Describe(new Azure.Identity.AuthenticationFailedException("sensitive token")));
        Assert.Equal("canceled or deadline exceeded", DemoErrors.Describe(new OperationCanceledException("sensitive URL")));
        Assert.Equal("Foundry request deadline exceeded", DemoErrors.Describe(new DecisionException("The Foundry request timed out.")));
    }

    private static FoundryDecisionOptions Options() => new()
    {
        Endpoint = new Uri("https://synthetic.example"), Model = "synthetic",
        ApiKey = "synthetic", Timeout = TimeSpan.FromSeconds(1)
    };

    private sealed class FakeClient : IDecisionClient
    {
        public bool Fail { get; init; }
        public Task<ChoiceDecision> ChooseAsync(string input, string instructions, IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Answer review never calls Choice or a generative model.");
        public Task<ScoreDecision> ScoreAsync(string input, string instructions, IReadOnlyList<string> rubric, CancellationToken cancellationToken = default) =>
            Fail ? throw new HttpRequestException("Synthetic failure") : Task.FromResult(Quality(2));
        public Task<AssessmentDecision> AssessAsync(string input, string proposition, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AssessmentDecision(1));
    }

    private sealed class BadResponse(string answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"answers\":{\"decision\":" + answer + "}}") });
    }

    private sealed class NativeStub : HttpMessageHandler
    {
        public List<string> Types { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            string state = document.RootElement.GetProperty("state").GetString()!;
            Assert.DoesNotContain("expectedStatus", state, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ready_for_review", state);
            var question = document.RootElement.GetProperty("questions").GetProperty("decision");
            string type = question.GetProperty("type").GetString()!;
            Types.Add(type);
            string answer;
            if (type == "score")
            {
                Assert.Equal(4, question.GetProperty("criteria").GetArrayLength());
                Assert.Equal(AnswerReviewer.QualityQuestion, question.GetProperty("instructions").GetString());
                answer = """{"type":"score","score":2.7,"probabilities":{"0":0,"1":0,"2":0.3,"3":0.7},"confidence":0.5}""";
            }
            else
            {
                Assert.Equal("noul", type);
                Assert.Equal(AnswerReviewer.SupportProposition, question.GetProperty("instructions").GetString());
                answer = """{"type":"noul","noul":0.9}""";
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"answers\":{\"decision\":" + answer + "}}") };
        }
    }
}
