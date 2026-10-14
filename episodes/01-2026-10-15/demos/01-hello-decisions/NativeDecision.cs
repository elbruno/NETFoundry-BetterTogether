/*
 * Goal: Show the native SystemOne wire contract without a chat API.
 * A. Serialize a named choice question using criteria and synthetic state.
 * B. Authenticate with Azure CLI or an optional external key and POST once.
 * C. Parse and validate the typed answer before returning it.
 * Inputs: Validated options, HttpClient, and synthetic input.
 * Output: The same ChoiceDecision used by the community SDK example.
 * Boundary: No retries, token logging, or endpoint/body dumps on errors.
 */
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using ElBruno.AI.Decisions;
using ElBruno.AI.Decisions.Foundry;

namespace HelloDecisions;

public sealed class NativeHttpException(int statusCode) : Exception("Native Foundry HTTP failure.")
{
    public int StatusCode { get; } = statusCode;
}

public static class NativeDecision
{
    // A. This matches Microsoft's documented choice contract, not chat completions.
    public static string RequestBody(string model, string input) => JsonSerializer.Serialize(new
    {
        model, state = input,
        questions = new { decision = new { type = "choice", instructions = HelloDecision.Instructions, criteria = HelloDecision.Options } }
    });

    public static async Task<ChoiceDecision> AskAsync(HttpClient http, FoundryDecisionOptions options,
        string input, CancellationToken token, TokenCredential? credential = null)
    {
        Uri endpoint = options.Endpoint!;
        if (endpoint.AbsolutePath == "/") endpoint = new Uri(endpoint, "providers/microsoft/v1/systemone");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(RequestBody(options.Model!, input), Encoding.UTF8, "application/json")
        };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(options.Timeout);

        // B. Never print headers or the resolved URL. Both transports use the same token audience.
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            request.Headers.Add("api-key", options.ApiKey);
        else
        {
            var accessToken = await (credential ?? new AzureCliCredential()).GetTokenAsync(
                new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), deadline.Token);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        }
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        if (!response.IsSuccessStatusCode)
            throw new NativeHttpException((int)response.StatusCode);

        // C. Bound response size even when Content-Length is missing or inaccurate.
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var body = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, deadline.Token)) > 0)
        {
            if (body.Length + count > 1_048_576) throw new InvalidDataException("Native response is too large.");
            body.Write(chunk, 0, count);
        }
        return Parse(Encoding.UTF8.GetString(body.ToArray()));
    }

    public static ChoiceDecision Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var answer = document.RootElement.GetProperty("answers").GetProperty("decision");
        if (answer.GetProperty("type").GetString() != "choice")
            throw new InvalidDataException("Expected a native choice answer.");
        var probabilities = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var property in answer.GetProperty("probabilities").EnumerateObject())
            if (!probabilities.TryAdd(property.Name, property.Value.GetDouble()))
                throw new InvalidDataException("Duplicate probability label.");
        var result = new ChoiceDecision(answer.GetProperty("choice").GetString()!,
            probabilities, answer.GetProperty("confidence").GetDouble());
        HelloDecision.Validate(result);
        return result;
    }
}
