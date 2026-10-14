/*
 * Goal: Make model-call failures visible without exposing resolved settings.
 * A. Recognize typed HTTP, identity and deadline failures.
 * B. Map safe metadata to fixed descriptions.
 * C. Report other exception types without printing raw diagnostic messages.
 * Inputs: A caught exception. Output: Sanitized failure category.
 * Boundary: Never log endpoint, tokens, response bodies or identity traces.
 */
using Azure.Identity;
using ElBruno.AI.Decisions;

namespace SupportTriage;

public static class DemoErrors
{
    // A/B/C. HTTP status is safe; raw provider messages and inner exceptions are not.
    public static string Describe(Exception error) => error switch
    {
        DecisionException { StatusCode: int status } => $"Foundry HTTP {status}",
        CredentialUnavailableException => "Azure CLI credential unavailable",
        AuthenticationFailedException => "Azure CLI authentication failed",
        OperationCanceledException => "canceled or deadline exceeded",
        DecisionException when error.Message == "The Foundry request timed out." => "Foundry request deadline exceeded",
        HttpRequestException => "HTTP transport failed",
        _ => error.GetType().Name
    };
}
