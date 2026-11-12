// Verify real service response shapes without calling Foundry or inspecting text locally.
using System.Net;
using System.Net.Http.Headers;
using System.Text;

const string promptBlock = """
    {"error":{"code":"content_filter","message":"Prompt filtered by the service","param":"prompt","innererror":{
    "code":"ResponsibleAIPolicyViolation","content_filter_result":{
    "custom_blocklists":{"details":[{"filtered":true,"id":"example"}],"filtered":true}}}}}
    """;
const string completionBlock = """
    {"choices":[{"finish_reason":"content_filter","content_filter_results":{
    "custom_blocklists":[{"filtered":true,"id":"example"}]}}]}
    """;
var prompt = FilterResult.Read(400, promptBlock);
Check(prompt is { Kind: FilterKind.ContentFiltered, Phase: "prompt blocked", CustomBlocklist: true },
    "prompt blocklist metadata");
Check(prompt?.Details.Contains("Prompt filtered by the service") == true, "service error message");
var completion = FilterResult.Read(200, completionBlock);
Check(completion is { Kind: FilterKind.ContentFiltered, Phase: "completion blocked", CustomBlocklist: true },
    "HTTP 200 completion blocklist metadata");
var generic = FilterResult.Read(400, """{"error":{"code":"content_filter","param":"prompt"}}""");
Check(generic is { Kind: FilterKind.ContentFiltered, CustomBlocklist: false }, "generic filtering");
Check(FilterResult.Read(400, """{"error":{"code":"invalid_request","message":"content_filter"}}""")
    is { Kind: FilterKind.OtherServiceError }, "unrelated HTTP 400");
foreach (var status in new[] { 401, 403 })
    Check(FilterResult.Read(status, "{}") is { Kind: FilterKind.Authentication }, $"HTTP {status}");
Check(FilterResult.Read(200, """{"choices":[{"finish_reason":"stop","message":{"content":"content_filter custom_blocklists"}}]}""")
    is null, "model text is not moderation metadata");
Check(FilterResult.Read(200, "{") is { Kind: FilterKind.OtherServiceError }, "invalid response JSON");
Check(FilterResult.Read(200, "[]") is { Kind: FilterKind.OtherServiceError }, "non-object response JSON");

using var capture = new ModerationCapture(new StubHandler(HttpStatusCode.OK, completionBlock, "application/json"));
using var client = new HttpClient(capture);
using var response = await client.GetAsync("https://example.test/");
Check(await response.Content.ReadAsStringAsync() == completionBlock, "observer preserves response body");
Check(capture.Result is { Kind: FilterKind.ContentFiltered, Phase: "completion blocked" }, "HTTP observer");
capture.Reset();
Check(capture.Result is null, "reset before the next request");

using var textCapture = new ModerationCapture(new StubHandler(HttpStatusCode.Forbidden, "forbidden", "text/plain"));
using var textClient = new HttpClient(textCapture);
using var textResponse = await textClient.GetAsync("https://example.test/");
Check(textCapture.Result is { Kind: FilterKind.Authentication }, "non-JSON authorization error");
Console.WriteLine("Guardrail response classification tests passed.");

static void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException($"Failed: {description}");
}

sealed class StubHandler(HttpStatusCode status, string body, string mediaType) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return Task.FromResult(new HttpResponseMessage(status) { Content = content });
    }
}
