/*
 * Goal: Report review failures without exposing deployment or identity details.
 * A. Identify typed HTTP, credential and deadline failures.
 * B. Select fixed descriptions from safe metadata.
 * C. Fall back to exception type without dumping provider diagnostics.
 * Inputs: A caught exception. Output: Sanitized failure category.
 * Boundary: No endpoint, response body, authentication headers or token logging.
 */
using Azure.Identity;
using ElBruno.AI.Decisions;

namespace AnswerReview;

public static class DemoErrors
{
    // A/B/C. Only known metadata is displayed; raw exception messages remain private.
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
