/*
 * Goal: Trace the native decision call, which is not an IChatClient call.
 * A. Nest a decision-model span inside the Framework agent span.
 * B. Record model metadata and only provider-reported token counters.
 * C. Mark failures without recording tickets, endpoints, or exception messages.
 */
using System.Diagnostics;
using Azure.Identity;
using ElBruno.AI.Decisions;

namespace SupportRouting;

public static class DecisionTelemetry
{
    public const string AgentSourceName = "Episode02.Agent";
    public const string SourceName = "Episode02.Decisions";
    private static readonly ActivitySource Source = new(SourceName);

    public static async Task<ChoiceDecision> ChooseAsync(
        IDecisionClient client, string deployment, string ticket, string question,
        IReadOnlyDictionary<string, string?> routes, CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity($"decision {deployment}", ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "decision");
        activity?.SetTag("gen_ai.provider.name", "microsoft");
        activity?.SetTag("gen_ai.request.model", deployment);

        try
        {
            var result = await client.ChooseAsync(ticket, question, routes, cancellationToken);
            if (result.Usage?.InputTokens is { } input)
                activity?.SetTag("gen_ai.usage.input_tokens", input);
            if (result.Usage?.OutputTokens is { } output)
                activity?.SetTag("gen_ai.usage.output_tokens", output);
            if (result.Usage?.ReasoningTokens is { } reasoning)
                activity?.SetTag("gen_ai.usage.reasoning.output_tokens", reasoning);
            if (result.Usage?.CachedTokens is { } cached)
                activity?.SetTag("gen_ai.usage.cache_read.input_tokens", cached);
            return result;
        }
        catch (Exception error) when (error is DecisionException or OperationCanceledException
            or HttpRequestException or AuthenticationFailedException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", error.GetType().Name);
            throw;
        }
    }
}
