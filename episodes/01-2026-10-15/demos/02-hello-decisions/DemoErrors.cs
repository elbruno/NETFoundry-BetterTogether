/*
 * Goal: Explain failures without exposing endpoint, token or response contents.
 * A. Recognize status codes and identity/deadline failures from typed exceptions.
 * B. Map only known metadata to fixed, public-safe descriptions.
 * C. Fall back to an exception type, never raw exception messages.
 * Inputs: A caught exception. Output: Sanitized console diagnostic.
 * Boundary: No request bodies, credentials or identity traces are emitted.
 */
using Azure.Identity;
using ElBruno.AI.Decisions;

namespace HelloDecisions;

public static class DemoErrors
{
    // A/B/C. Status codes are safe; messages and inner exceptions can contain resolved settings.
    public static string Describe(Exception error) => error switch
    {
        NativeHttpException native => $"Foundry HTTP {native.StatusCode}",
        DecisionException { StatusCode: int status } => $"Foundry HTTP {status}",
        CredentialUnavailableException => "Azure CLI credential unavailable",
        AuthenticationFailedException => "Azure CLI authentication failed",
        OperationCanceledException => "canceled or deadline exceeded",
        DecisionException when error.Message == "The Foundry request timed out." => "Foundry request deadline exceeded",
        HttpRequestException => "HTTP transport failed",
        _ => error.GetType().Name
    };
}
