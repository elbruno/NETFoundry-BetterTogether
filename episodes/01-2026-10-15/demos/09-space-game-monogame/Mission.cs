/*
 * Goal: Keep window input responsive while one asynchronous decision is pending.
 * A. Select providers explicitly and invalidate work on takeover or restart.
 * B. Advance four-tick actions with a separate 50 ms simulation clock.
 * C. Pause on focus loss and bound AI missions without automatic retries.
 */
using ElBruno.AI.Decisions;

namespace DecisionSpaceGame;

public sealed class Mission(
    Func<IDecisionClient> foundry, Func<IDecisionClient> ollama,
    string foundryModel = "Microsoft-Decision-1", string ollamaModel = "nimble") : IAsyncDisposable
{
    private readonly Dictionary<string, IDecisionClient> clients = [];
    private double accumulator;
    private int nextManualTick;
    private bool usedAi;
    public Game World { get; private set; } = new(42);
    public DecisionDriver Driver { get; } = new(TimeSpan.FromSeconds(60), Game.ActionTicks);
    public bool Welcome { get; private set; } = true;
    public bool Paused { get; private set; }
    public bool FocusPaused { get; private set; }
    public string? Outcome { get; private set; }
    public double WallSeconds { get; private set; }
    public double? FinalWallSeconds { get; private set; }
    public bool Suspended => Welcome || Paused || FocusPaused || Outcome is not null;

    public void Select(string provider)
    {
        if (Outcome is not null && Outcome != "provider failure") return;
        if (Outcome == "provider failure") { Outcome = null; FinalWallSeconds = null; }
        Welcome = false;
        accumulator = 0;
        if (provider == "manual")
        {
            Driver.Switch(World, "manual", "keyboard", null);
            nextManualTick = World.Tick;
            return;
        }
        usedAi = true;
        string model = provider == "foundry" ? foundryModel : ollamaModel;
        try
        {
            if (!clients.TryGetValue(provider, out var client))
            {
                client = (provider == "foundry" ? foundry : ollama)();
                clients.Add(provider, client);
            }
            Driver.Switch(World, provider, model, client);
        }
        catch (Exception)
        {
            Driver.Switch(World, provider, model, null,
                $"Cannot configure {provider}. Check settings off stream. M: manual");
        }
    }

    public void Restart()
    {
        Driver.ResetStatistics();
        World = new Game(42);
        Outcome = null;
        FinalWallSeconds = null;
        WallSeconds = 0;
        usedAi = false;
        Paused = FocusPaused = false;
        Select("manual");
    }

    public void LoseFocus()
    {
        if (Welcome || Outcome is not null || FocusPaused) return;
        FocusPaused = true;
        Driver.Statistics.RecordPause();
        accumulator = 0;
    }

    public void TogglePause()
    {
        if (Welcome || Outcome is not null) return;
        if (FocusPaused) { FocusPaused = false; Paused = false; }
        else
        {
            Paused = !Paused;
            if (Paused) Driver.Statistics.RecordPause();
        }
        accumulator = 0;
    }

    public void Update(double seconds, GameAction manualAction = GameAction.Stop, double? wallElapsedSeconds = null)
    {
        if (!Welcome && Outcome is null) WallSeconds += wallElapsedSeconds ?? seconds;
        if (Outcome is null && usedAi && WallSeconds >= 300) Finish("AI wall-time limit");
        bool thinking = Driver.HasPendingRequest;
        Driver.Update(World, Suspended);
        if (Driver.Failed && !Welcome && Outcome is null)
        {
            Finish("provider failure");
            return;
        }
        if (Suspended || Driver.HasPendingRequest || (Driver.Provider != "manual" && Driver.Failed))
        {
            accumulator = 0;
            return;
        }
        // Completion starts a fresh simulation interval, not a catch-up from inference.
        if (thinking) { accumulator = 0; return; }
        accumulator += seconds * 1000;
        while (accumulator >= Game.StepMilliseconds && !Suspended)
        {
            if (Driver.Provider == "manual" && World.Tick >= nextManualTick)
            {
                World.Apply(manualAction);
                nextManualTick = World.Tick + Game.ActionTicks;
            }
            World.Step();
            accumulator -= Game.StepMilliseconds;
            if (World.Finished) Finish(World.Lives > 0 ? "victory" : "defeat");
            else if (usedAi && World.Tick >= 1200) Finish("AI simulation limit");
            else if (Driver.Provider != "manual" && !World.HasActiveAction)
            {
                accumulator = 0;
                break;
            }
        }
    }

    public void Finish(string outcome)
    {
        if (Outcome is not null) return;
        Outcome = outcome;
        FinalWallSeconds = WallSeconds;
        Driver.Cancel();
        accumulator = 0;
    }

    public async ValueTask DisposeAsync()
    {
        await Driver.DisposeAsync();
        foreach (var client in clients.Values.OfType<IDisposable>()) client.Dispose();
        clients.Clear();
    }
}
