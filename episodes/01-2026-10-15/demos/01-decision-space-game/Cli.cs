/*
 * Goal: Make runtime choices explicit and keep rehearsal runs bounded.
 * A. Define readable help and safe defaults.
 * B. Parse the requested provider, timing, and execution mode.
 * C. Reject invalid options before configuration or model calls.
 * Inputs: Command-line arguments.
 * Output: Validated options or an actionable argument error.
 * Boundary: Selecting replay is explicit, never an authentication fallback.
 */
using System.Globalization;

namespace DecisionSpaceGame;

public sealed record Cli(string Provider, bool Headless, bool Probe, bool Help,
    double Seconds, int Seed, double TimeoutSeconds, double MaxAgeSeconds)
{
    public const string HelpText = """
        Decision Space Game (.NET 10) - original native bounded-decision demo
        --provider manual|ollama|foundry|replay  (default manual)
        --headless                              no keyboard or animated board
        --seconds 0.1..3600                     bounded wall-clock duration (default 60)
        --seed <integer>                       deterministic simulation seed (default 42)
        --timeout-seconds 0.1..120              per-request cancellation (default 5)
        --max-age-seconds 0.05..120             reject older snapshots (default 1.5)
        --probe                                one fixed initial-state request, no game loop
        --help
        Controls: Left/A, Right/D, Space=fire, S=stop, Q/Escape=quit.
        M=manual (explicit fallback), O=Ollama, F=Foundry, R=OFFLINE replay.
        Failed provider pauses AI until you explicitly select a provider again.
        Foundry:Endpoint, Foundry:Model, optional Foundry:ApiKey.
        Ollama:Endpoint (default localhost:11434), Ollama:Model (required).
        User secrets or env vars: replace ':' with '__' for environment keys.
        """;

    public static Cli Parse(string[] args)
    {
        string provider = "manual";
        bool headless = false, probe = false, help = false;
        double seconds = 60, timeout = 5, age = 1.5;
        int seed = 42;
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            string Value() => ++i < args.Length ? args[i] :
                throw new ArgumentException($"Missing value for {option}.");
            switch (option)
            {
                case "--provider": provider = Value(); break;
                case "--headless": headless = true; break;
                case "--probe": probe = true; break;
                case "--help": help = true; break;
                case "--seconds": seconds = Number(Value(), option, 0.1, 3600); break;
                case "--timeout-seconds": timeout = Number(Value(), option, 0.1, 120); break;
                case "--max-age-seconds": age = Number(Value(), option, 0.05, 120); break;
                case "--seed":
                    if (!int.TryParse(Value(), CultureInfo.InvariantCulture, out seed))
                        throw new ArgumentException("--seed must be an integer.");
                    break;
                default: throw new ArgumentException($"Unknown option: {option}.");
            }
        }
        if (provider is not ("manual" or "ollama" or "foundry" or "replay"))
            throw new ArgumentException("--provider must be manual, ollama, foundry or replay.");
        if (probe && provider == "manual") throw new ArgumentException("--probe needs ollama, foundry or replay.");
        return new(provider, headless, probe, help, seconds, seed, timeout, age);
    }

    private static double Number(string value, string name, double min, double max) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) &&
        double.IsFinite(number) && number >= min && number <= max
            ? number : throw new ArgumentException($"{name} must be {min}..{max}.");
}
