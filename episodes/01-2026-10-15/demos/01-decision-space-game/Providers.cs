/*
 * Goal: Select a native decision client without changing the game's rules.
 * A. Read the selected provider's external configuration.
 * B. Construct and cache its native SystemOne client.
 * C. Dispose clients when the bounded run ends.
 * Inputs: Configuration and a request timeout.
 * Output: A model label and IDecisionClient, or explicit manual control.
 * Boundary: Replay is synthetic, clearly labeled, and never a silent fallback.
 */
using ElBruno.AI.Decisions;
using ElBruno.AI.Decisions.Foundry;
using ElBruno.AI.Decisions.Ollama;
using Microsoft.Extensions.Configuration;

namespace DecisionSpaceGame;

public sealed class Providers(IConfiguration config, TimeSpan timeout) : IDisposable
{
    private readonly Dictionary<string, (string Model, IDecisionClient Client)> _clients = [];

    public (string Model, IDecisionClient? Client) Get(string provider)
    {
        if (provider == "manual") return ("keyboard", null);
        if (_clients.TryGetValue(provider, out var cached)) return cached;
        string model;
        IDecisionClient client;
        switch (provider)
        {
            case "replay":
                model = "OFFLINE scripted actions (NOT a model)";
                client = new ReplayClient();
                break;
            case "foundry":
                // A/B. Model is the deployment name; this is not IChatClient.
                model = Required("Foundry:Model");
                client = new FoundryDecisionClient(new FoundryDecisionOptions
                {
                    Endpoint = Endpoint("Foundry:Endpoint", null, requireHttps: true),
                    Model = model, ApiKey = config["Foundry:ApiKey"], Timeout = timeout
                });
                break;
            case "ollama":
                model = Required("Ollama:Model");
                client = new OllamaDecisionClient(new OllamaDecisionOptions
                {
                    Endpoint = Endpoint("Ollama:Endpoint", "http://localhost:11434/", requireHttps: false),
                    Model = model, Timeout = timeout
                });
                break;
            default:
                throw new ArgumentException("Unknown provider.");
        }
        _clients.Add(provider, (model, client));
        return (model, client);
    }

    private string Required(string key) => !string.IsNullOrWhiteSpace(config[key])
        ? config[key]! : throw new ArgumentException($"Set {key} in user secrets or environment variables.");

    private Uri Endpoint(string key, string? fallback, bool requireHttps)
    {
        string text = config[key] ?? fallback ?? Required(key);
        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) ||
            (requireHttps ? uri.Scheme != "https" : uri.Scheme is not ("http" or "https")) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException($"{key} must be an absolute {(requireHttps ? "HTTPS" : "HTTP(S)")} URL without credentials, query or fragment.");
        return uri;
    }

    public void Dispose()
    {
        // C. Owned clients are reused during play and released once.
        foreach (var entry in _clients.Values)
            (entry.Client as IDisposable)?.Dispose();
    }
}

public sealed class ReplayClient : IDecisionClient
{
    private int _index;
    private static readonly string[] Script = ["fire", "move_left", "fire", "move_right", "stop", "fire"];

    public async Task<ChoiceDecision> ChooseAsync(string input, string instructions,
        IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
    {
        await Task.Delay(80, cancellationToken);
        string label = Script[_index++ % Script.Length];
        return new ChoiceDecision(label, options.Keys.ToDictionary(k => k, k => k == label ? 1.0 : 0.0), 1);
    }

    public Task<ScoreDecision> ScoreAsync(string input, string instructions, IReadOnlyList<string> rubric,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AssessmentDecision> AssessAsync(string input, string proposition,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
