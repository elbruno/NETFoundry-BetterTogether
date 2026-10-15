/*
 * Goal: Keep configuration out of the game screen and defer clients until chosen.
 * A. Read user secrets and environment overrides at startup.
 * B. Construct a provider only after F or O is pressed.
 * C. Never display endpoints, keys, or raw provider diagnostics.
 */
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using Azure;
using Azure.Identity;
using ElBruno.AI.Decisions;
using ElBruno.AI.Decisions.Foundry;
using ElBruno.AI.Decisions.Ollama;
using Microsoft.Extensions.Configuration;

namespace PlatformerDecisions;

public static class DecisionProviderFactory
{
    private static IConfiguration? configuration;

    public static void Configure(IConfiguration values) => configuration = values;

    public static string ModelLabel(string provider)
    {
        IConfiguration config = configuration ?? throw new InvalidOperationException("Configuration is unavailable.");
        return provider switch
        {
            "foundry" => config["Foundry:Model"] ?? "Microsoft-Decision-1 (deployment not configured)",
            "ollama" => config["Ollama:Model"] ?? "nimble:latest",
            _ => "keyboard"
        };
    }

    public static IDecisionClient Create(string provider)
    {
        IConfiguration config = configuration ?? throw new InvalidOperationException("Configuration is unavailable.");
        return provider switch
        {
            "foundry" => new FoundryDecisionClient(new FoundryDecisionOptions
            {
                Endpoint = new Uri(config["Foundry:Endpoint"]
                    ?? throw new InvalidOperationException("Configure Foundry with the episode setup script.")),
                Model = config["Foundry:Model"]
                    ?? throw new InvalidOperationException("Configure the Foundry deployment with the episode setup script."),
                ApiKey = config["Foundry:ApiKey"],
                Timeout = TimeSpan.FromSeconds(30)
            }),
            "ollama" => new OllamaDecisionClient(new OllamaDecisionOptions
            {
                Endpoint = new Uri(config["Ollama:Endpoint"] ?? "http://localhost:11434"),
                Model = config["Ollama:Model"] ?? "nimble:latest",
                Timeout = TimeSpan.FromSeconds(30)
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }
}

internal sealed record DecisionAttempt(ChoiceDecision? Decision, string? Error, double DurationMs);

internal static class DecisionCalls
{
    public static async Task<DecisionAttempt> ChooseAsync(IDecisionClient client, string state, CancellationToken token)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            ChoiceDecision decision = await client.ChooseAsync(state, ControlAction.Instructions,
                ControlAction.Options, token).ConfigureAwait(false);
            return new(decision, null, watch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return new(null, "Request cancelled or timed out.", watch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is DecisionException or HttpRequestException or
            JsonException or AuthenticationFailedException or RequestFailedException)
        {
            return new(null, $"Provider request failed ({ex.GetType().Name}). Check setup off stream.", watch.Elapsed.TotalMilliseconds);
        }
    }
}
