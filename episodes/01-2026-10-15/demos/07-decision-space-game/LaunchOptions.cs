/*
 * Goal: Keep no-argument play simple, with two optional auto-start switches.
 * A. Accept manual launch, --foundry, or --ollama.
 * B. Reject ambiguous/unknown arguments without making any model calls.
 * C. Use identical seed and bounded mission rules for two-console rehearsals.
 */
namespace DecisionSpaceGame;

public sealed record LaunchOptions(string Provider = "manual", bool AutoFinish = false)
{
    public const int MaxAutoTicks = 1200;
    public const double MaxAutoSeconds = 300;
    public const string Usage = "dotnet run [-- --foundry | -- --ollama]\nNo arguments: manual start, M/F/O switch. --help: usage.";

    public static LaunchOptions Parse(string[] args) => args switch
    {
        [] => new(),
        ["--foundry"] => new("foundry", true),
        ["--ollama"] => new("ollama", true),
        _ => throw new ArgumentException("Choose exactly one of --foundry or --ollama, or no arguments.")
    };
}
