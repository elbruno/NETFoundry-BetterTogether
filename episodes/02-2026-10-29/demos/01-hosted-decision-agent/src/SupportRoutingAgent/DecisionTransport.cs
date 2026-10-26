/*
 * Goal: Observe the native call without recording tickets or credentials.
 * A. Leave authentication and request construction to the decision SDK.
 * B. Record only HTTP status.
 * C. Preserve the response for the SDK's normal validation and error handling.
 */
namespace SupportRouting;

public sealed class DecisionTransport(ILogger logger) : DelegatingHandler(
    new SocketsHttpHandler { AllowAutoRedirect = false })
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        logger.LogInformation("Native decision HTTP {StatusCode}", (int)response.StatusCode);
        return response;
    }
}
