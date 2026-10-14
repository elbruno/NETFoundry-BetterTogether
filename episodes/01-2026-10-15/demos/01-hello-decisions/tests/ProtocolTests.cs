/*
 * Goal: Verify SDK/REST contracts without cloud credentials or billable requests.
 * A. Supply synthetic HTTP answers and fake identity tokens.
 * B. Run both transports and inspect their serialized requests.
 * C. Assert typed results, failure boundaries, and configuration validation.
 * Inputs: In-memory fixtures only. Output: Deterministic protocol tests.
 * Boundary: These tests do not prove live model quality or calibration.
 */
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HelloDecisions.Tests;

public sealed class ProtocolTests
{
    // A. Confidence deliberately differs from the selected-label probability.
    private const string Answer = """
        {"answers":{"decision":{"type":"choice","choice":"billing","probabilities":{"billing":0.8,"technical_support":0.15,"sales":0.05},"confidence":0.6}}}
        """;
    private static FoundryDecisionOptions Options(string path = "/") => new()
    {
        Endpoint = new Uri("https://synthetic.example" + path),
        Model = "synthetic-deployment", ApiKey = "synthetic-test-key", Timeout = TimeSpan.FromSeconds(1)
    };

    [Theory]
    [InlineData(false, "/")]
    [InlineData(true, "/")]
    [InlineData(true, "/providers/microsoft/v1/systemone")]
    public async Task BothTransportsUseNativeNamedCriteria(bool rest, string path)
    {
        var handler = new Stub(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/providers/microsoft/v1/systemone", request.RequestUri!.AbsolutePath);
            Assert.Equal("synthetic-test-key", request.Headers.GetValues("api-key").Single());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal("synthetic-deployment", body.RootElement.GetProperty("model").GetString());
            Assert.Equal(HelloDecision.Input("billing"), body.RootElement.GetProperty("state").GetString());
            var question = body.RootElement.GetProperty("questions").GetProperty("decision");
            Assert.Equal("choice", question.GetProperty("type").GetString());
            Assert.Equal(3, question.GetProperty("criteria").EnumerateObject().Count());
            Assert.Equal(HelloDecision.Instructions, question.GetProperty("instructions").GetString());
            return Json(Answer);
        });
        using var http = new HttpClient(handler);
        using var sdk = new FoundryDecisionClient(http, Options(path));
        // B. The exact same response is consumed by the SDK and our native path.
        var result = rest
            ? await NativeDecision.AskAsync(http, Options(path), HelloDecision.Input("billing"), default)
            : await HelloDecision.AskAsync(sdk, HelloDecision.Input("billing"), default);
        // C. Do not conflate confidence with probability.
        Assert.Equal("billing", result.Choice);
        Assert.Equal(0.8, result.Probabilities["billing"]);
        Assert.Equal(0.6, result.Confidence);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"answers\":{\"decision\":{\"type\":\"noul\"}}}")]
    [InlineData("{\"answers\":{\"decision\":{\"type\":\"choice\",\"choice\":\"tool\",\"probabilities\":{\"tool\":1},\"confidence\":1}}}")]
    [InlineData("{\"answers\":{\"decision\":{\"type\":\"choice\",\"choice\":\"billing\",\"probabilities\":{\"billing\":0.5,\"technical_support\":0.1,\"sales\":0.1},\"confidence\":1}}}")]
    [InlineData("{\"answers\":{\"decision\":{\"type\":\"choice\",\"choice\":\"billing\",\"probabilities\":{\"billing\":1,\"billing\":1,\"technical_support\":0,\"sales\":0},\"confidence\":1}}}")]
    [InlineData("{\"answers\":{\"decision\":{\"type\":\"choice\",\"choice\":\"billing\",\"probabilities\":{\"billing\":1,\"technical_support\":0,\"sales\":0},\"confidence\":2}}}")]
    public void NativeRejectsMalformedAnswers(string json) =>
        Assert.NotNull(Record.Exception(() => NativeDecision.Parse(json)));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpAuthenticationFailureDoesNotReturnADecision(bool rest)
    {
        using var http = new HttpClient(new Stub((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("sensitive body must not be printed") })));
        using var sdk = new FoundryDecisionClient(http, Options());
        var error = await Record.ExceptionAsync(async () =>
        {
            if (rest) await NativeDecision.AskAsync(http, Options(), "synthetic", default);
            else await HelloDecision.AskAsync(sdk, "synthetic", default);
        });
        Assert.NotNull(error);
        Assert.DoesNotContain("sensitive body", error.Message);
        Assert.Equal("Foundry HTTP 401", DemoErrors.Describe(error));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedHttpResponseIsRejectedByBothTransports(bool rest)
    {
        using var http = new HttpClient(new Stub((_, _) => Task.FromResult(Json(
            Answer.Replace("\"billing\":0.8", "\"billing\":0.3", StringComparison.Ordinal)))));
        using var sdk = new FoundryDecisionClient(http, Options());
        Assert.NotNull(await Record.ExceptionAsync(async () =>
        {
            if (rest) await NativeDecision.AskAsync(http, Options(), "synthetic", default);
            else await HelloDecision.AskAsync(sdk, "synthetic", default);
        }));
    }

    [Fact]
    public void ErrorDescriptionsNeverExposeRawDiagnosticMessages()
    {
        Assert.Equal("Azure CLI authentication failed", DemoErrors.Describe(new Azure.Identity.AuthenticationFailedException("sensitive identity details")));
        Assert.Equal("canceled or deadline exceeded", DemoErrors.Describe(new OperationCanceledException("sensitive URL")));
        Assert.Equal("InvalidDataException", DemoErrors.Describe(new InvalidDataException("sensitive response")));
        Assert.Equal("Foundry request deadline exceeded", DemoErrors.Describe(new ElBruno.AI.Decisions.DecisionException("The Foundry request timed out.")));
    }

    [Fact]
    public async Task NativeUsesCliCompatibleAudienceWhenNoKeySupplied()
    {
        var credential = new SyntheticCredential();
        using var http = new HttpClient(new Stub((request, _) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.False(request.Headers.Contains("api-key"));
            return Task.FromResult(Json(Answer));
        }));
        var options = Options();
        options.ApiKey = null;
        await NativeDecision.AskAsync(http, options, "synthetic", default, credential);
        Assert.Equal("https://cognitiveservices.azure.com/.default", credential.Scope);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationStopsTransport(bool rest)
    {
        using var http = new HttpClient(new Stub(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Json(Answer);
        }));
        using var sdk = new FoundryDecisionClient(http, Options());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        Assert.NotNull(await Record.ExceptionAsync(async () =>
        {
            if (rest) await NativeDecision.AskAsync(http, Options(), "synthetic", cancellation.Token);
            else await HelloDecision.AskAsync(sdk, "synthetic", cancellation.Token);
        }));
    }

    [Theory]
    [InlineData(null, "deployment")]
    [InlineData("http://synthetic.example", "deployment")]
    [InlineData("https://synthetic.example/projects/demo", "deployment")]
    [InlineData("https://synthetic.example?key=value", "deployment")]
    [InlineData("https://user:pass@synthetic.example", "deployment")]
    [InlineData("https://synthetic.example", "")]
    public void ConfigurationRejectsUnsafeOrMissingSettings(string? endpoint, string model)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Foundry:Endpoint"] = endpoint, ["Foundry:Model"] = model }).Build();
        Assert.Throws<ArgumentException>(() => DemoConfiguration.Read(config));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Stub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return send(request, token);
        }
    }

    private sealed class SyntheticCredential : TokenCredential
    {
        public string? Scope { get; private set; }
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This demo uses async identity acquisition.");
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Scope = Assert.Single(requestContext.Scopes);
            return ValueTask.FromResult(new AccessToken("synthetic-token", DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }
}
