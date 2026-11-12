// Goal: Observe service metadata, not filter user text or generated content.
// A: Read the SDK's buffered HTTP JSON response without changing it.
// B: Classify only structured service error and finish-reason fields.
// C: Distinguish custom-blocklist evidence from generic content filtering.
using System.Text.Json;

internal sealed class ModerationCapture : DelegatingHandler
{
    public ModerationCapture() : this(new HttpClientHandler()) { }
    internal ModerationCapture(HttpMessageHandler innerHandler) : base(innerHandler) { }

    public FilterResult? Result { get; private set; }
    public void Reset() => Result = null;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var status = (int)response.StatusCode;
        if (string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json",
            StringComparison.OrdinalIgnoreCase))
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Result = FilterResult.Read(status, body);
        }
        else if (status >= 400)
            Result = FilterResult.Unparseable(status, "response was not JSON");
        return response;
    }
}

internal enum FilterKind { ContentFiltered, Authentication, OtherServiceError }

internal sealed record FilterResult(FilterKind Kind, string Phase, bool CustomBlocklist, string Details)
{
    public override string ToString() =>
        $"{Kind}: {Phase}; {(CustomBlocklist ? "custom blocklist identified" : "custom blocklist not confirmed")}; {Details}";

    public static FilterResult? Read(int status, string body)
    {
        if (status is 401 or 403)
            return new(FilterKind.Authentication, "authentication or authorization", false, $"HTTP {status}");

        JsonDocument document;
        try { document = JsonDocument.Parse(body); }
        catch (JsonException) { return Unparseable(status, "invalid response JSON"); }
        using (document)
        {
            return ReadObject(status, document.RootElement);
        }
    }

    public static FilterResult Unparseable(int status, string reason) =>
        status is 401 or 403
            ? new(FilterKind.Authentication, "authentication or authorization", false, $"HTTP {status}; {reason}")
            : new(FilterKind.OtherServiceError, "service response could not be inspected", false, $"HTTP {status}; {reason}");

    private static FilterResult? ReadObject(int status, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return Unparseable(status, "expected a JSON object");
        var hasError = root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object;
        var code = hasError ? Text(error, "code") : null;
        var message = hasError ? Text(error, "message") : null;
        var messageDetails = message is null ? "" : $"; message={message}";
        var parameter = hasError ? Text(error, "param") : null;
        var inner = hasError && error.TryGetProperty("innererror", out var nested) ? nested : default;
        var innerCode = Text(inner, "code");
        var errorFiltered = code is "content_filter" or "ResponsibleAIPolicyViolation"
            || innerCode == "ResponsibleAIPolicyViolation";
        var completionFiltered = false;
        var custom = hasError && HasCustomBlocklist(error);
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                completionFiltered |= Text(choice, "finish_reason") == "content_filter";
                custom |= HasCustomBlocklist(choice);
            }
        }
        if (root.TryGetProperty("incomplete_details", out var incomplete))
            completionFiltered |= Text(incomplete, "reason") == "content_filter";

        if (errorFiltered || completionFiltered || custom)
        {
            var phase = completionFiltered || parameter == "completion" ? "completion blocked"
                : parameter == "prompt" ? "prompt blocked" : "filter stage not reported";
            return new(FilterKind.ContentFiltered, phase, custom,
                $"HTTP {status}; code={code ?? "not reported"}; innercode={innerCode ?? "not reported"}; completion content_filter={completionFiltered}{messageDetails}");
        }
        if (status >= 400)
            return new(FilterKind.OtherServiceError, "service error, not confirmed filtering", false,
                $"HTTP {status}; code={code ?? "not reported"}{messageDetails}");
        return null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool HasCustomBlocklist(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name == "custom_blocklists")
                return IsFiltered(property.Value);
            if (property.Value.ValueKind == JsonValueKind.Object && HasCustomBlocklist(property.Value))
                return true;
        }
        return false;
    }

    private static bool IsFiltered(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Any(IsFiltered);
        return value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("filtered", out var filtered)
            && filtered.ValueKind == JsonValueKind.True;
    }
}
