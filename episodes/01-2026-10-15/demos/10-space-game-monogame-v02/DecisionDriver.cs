/*
 * Goal: Apply a native model's choice safely, one decision-paced turn at a time.
 * A. Capture immutable state and start at most one request.
 * B. Observe completion, timeout, or a provider switch.
 * C. Reject stale/illegal suggestions or apply a validated action.
 * Inputs: Game snapshots, a decision client, and cancellation limits.
 * Output: Validated actions plus explicit status and latency.
 * Read next: Game.cs defines the rules that no model can override.
 * Boundary: Only the simulation thread changes game state.
 */
using System.Diagnostics;
using Azure.Identity;
using ElBruno.AI.Decisions;

namespace DecisionSpaceGame;

public sealed record DecisionAttempt(ChoiceDecision? Decision, string? Error, double LatencyMs);

public static class DecisionCalls
{
    public static async Task<DecisionAttempt> ChooseAsync(IDecisionClient client, string snapshot,
        CancellationToken token, IReadOnlyDictionary<string, string?>? options = null)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await client.ChooseAsync(snapshot, Actions.Instructions, options ?? Actions.Options, token);
            return new(result, null, watch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return new(null, "Request cancelled or timed out.", watch.Elapsed.TotalMilliseconds);
        }
        catch (DecisionException ex)
        {
            return new(null, ex.StatusCode is { } code ? $"Provider returned HTTP {code}." :
                "Provider failed: transport, timeout, or invalid SystemOne response.", watch.Elapsed.TotalMilliseconds);
        }
        catch (HttpRequestException)
        {
            return new(null, "HTTP transport failed.", watch.Elapsed.TotalMilliseconds);
        }
        catch (AuthenticationFailedException)
        {
            return new(null, "Azure authentication failed. Check az login and deployment access.", watch.Elapsed.TotalMilliseconds);
        }
        catch (Exception)
        {
            return new(null, "Provider failed. Check configuration off stream.", watch.Elapsed.TotalMilliseconds);
        }
    }
}

// All mutable driver and game state belongs to the simulation thread. The worker only returns a result.
public sealed class DecisionDriver : IAsyncDisposable
{
    private Task<DecisionAttempt>? _pending;
    private CancellationTokenSource? _requestCancellation;
    private IDecisionClient? _client;
    private IReadOnlyDictionary<string, string?> _requestOptions = Actions.Options;
    private int _generation, _requestGeneration, _snapshotTick, _nextRequestTick;
    private readonly TimeSpan _timeout;
    private readonly int _maxAgeTicks;
    private MissionStatistics? _requestStatistics;

    public DecisionDriver(TimeSpan timeout, int maxAgeTicks)
    {
        _timeout = timeout;
        _maxAgeTicks = maxAgeTicks;
    }

    public string Provider { get; private set; } = "manual";
    public string Model { get; private set; } = "keyboard";
    public string Status { get; private set; } = "Manual controls";
    public bool Failed { get; private set; }
    public bool HasPendingRequest => _pending is not null;
    public DecisionAttempt? LastAttempt { get; private set; }
    public int Accepted { get; private set; }
    public int Rejected { get; private set; }
    public MissionStatistics Statistics { get; private set; } = new();
    public void ResetStatistics() => Statistics = new();
    public void Cancel()
    {
        _generation++;
        _requestCancellation?.Cancel();
    }

    public void Switch(Game game, string provider, string model, IDecisionClient? client, string? error = null)
    {
        _generation++;
        _requestCancellation?.Cancel();
        game.Apply(GameAction.Stop);
        Provider = provider;
        Statistics.Select(provider);
        Model = model;
        _client = client;
        Failed = error is not null;
        LastAttempt = null;
        Accepted = Rejected = 0;
        Status = error ?? (client is null ? "Manual controls" : "Ready");
        _nextRequestTick = game.Tick;
    }

    public void Update(Game game, bool suspended = false)
    {
        // Paused results remain pending until explicit resume; old generations may drain.
        if (suspended && _requestGeneration == _generation) return;
        if (_pending is { IsCompleted: false } && _requestGeneration == _generation &&
            _requestCancellation!.IsCancellationRequested && !Failed)
        {
            Failed = true;
            Status = "Request timed out; waiting for cancellation.";
            game.Apply(GameAction.Stop);
        }
        // B. Provider switches invalidate old work even if cancellation arrives late.
        if (_pending is { IsCompleted: true })
        {
            DecisionAttempt attempt = _pending.GetAwaiter().GetResult();
            bool cancelled = _requestCancellation!.IsCancellationRequested;
            _pending = null;
            _requestCancellation.Dispose();
            _requestCancellation = null;
            string disposition;
            if (_requestGeneration == _generation)
            {
                LastAttempt = attempt;
                if (cancelled || attempt.Error is not null)
                {
                    disposition = cancelled ? "cancelled" : "failed";
                    Failed = true;
                    Status = cancelled ? "Request timed out." : attempt.Error!;
                    game.Apply(GameAction.Stop);
                }
                // C. A valid label can still be unsafe for a newer game state.
                else if (game.Tick - _snapshotTick > _maxAgeTicks || game.Finished)
                {
                    disposition = "rejected";
                    Rejected++;
                    Status = "Stale decision discarded";
                }
                else if (!IsLegal(attempt.Decision!, game, out GameAction action, _requestOptions))
                {
                    disposition = "rejected";
                    Rejected++;
                    Failed = true;
                    Status = "Illegal decision discarded";
                }
                else
                {
                    disposition = "accepted";
                    game.Apply(action);
                    _nextRequestTick = game.Tick + Game.ActionTicks;
                    Accepted++;
                    Status = $"Applied {attempt.Decision!.Choice}";
                }
            }
            else
            {
                disposition = attempt.Decision is null ? "cancelled" : "rejected";
                Rejected++;
                Status = Failed ? Status : "Old provider decision discarded";
            }
            _requestStatistics!.Complete(attempt, disposition);
        }
        if (suspended || _pending is not null || _client is null || Failed || game.Finished || game.Tick < _nextRequestTick)
            return;
        // A. Capture before leaving the simulation thread, never share mutable state.
        _snapshotTick = game.Tick;
        _requestGeneration = _generation;
        _nextRequestTick = game.Tick + Game.ActionTicks;
        _requestCancellation = new CancellationTokenSource(_timeout);
        _requestStatistics = Statistics;
        Statistics.StartRequest();
        IDecisionClient client = _client;
        string snapshot = game.Snapshot();
        _requestOptions = game.AllowedOptions();
        var options = _requestOptions;
        CancellationToken token = _requestCancellation.Token;
        _pending = Task.Run(() => DecisionCalls.ChooseAsync(client, snapshot, token, options));
    }

    public static bool IsLegal(ChoiceDecision decision, Game game, out GameAction action,
        IReadOnlyDictionary<string, string?>? requestedOptions = null)
    {
        var options = requestedOptions ?? Actions.Options;
        if (!Actions.TryMap(decision.Choice, out action)) return false;
        if (!options.ContainsKey(decision.Choice) ||
            decision.Probabilities.Count != options.Count ||
            options.Keys.Any(key => !decision.Probabilities.ContainsKey(key)) ||
            decision.Probabilities.Values.Any(value => !double.IsFinite(value) || value is < 0 or > 1) ||
            Math.Abs(decision.Probabilities.Values.Sum() - 1) > 0.01)
            return false;
        return action switch
        {
            GameAction.MoveLeft => game.PlayerX > 0,
            GameAction.MoveRight => game.PlayerX < Game.Width - 1,
            GameAction.Fire => game.FireCooldownTicks == 0,
            _ => true
        };
    }

    public async ValueTask DisposeAsync()
    {
        _requestCancellation?.Cancel();
        if (_pending is not null)
        {
            var attempt = await _pending;
            _requestStatistics!.Complete(attempt, "cancelled");
            _pending = null;
        }
        _requestCancellation?.Dispose();
        _requestCancellation = null;
    }
}
