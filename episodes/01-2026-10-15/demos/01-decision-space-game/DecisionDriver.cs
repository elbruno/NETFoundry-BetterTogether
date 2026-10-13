using System.Diagnostics;
using Azure.Identity;
using ElBruno.AI.Decisions;

namespace DecisionSpaceGame;

public sealed record DecisionAttempt(ChoiceDecision? Decision, string? Error, double LatencyMs);

public static class DecisionCalls
{
    public static async Task<DecisionAttempt> ChooseAsync(IDecisionClient client, string snapshot,
        CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await client.ChooseAsync(snapshot, Actions.Instructions, Actions.Options, token);
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
    }
}

// All mutable driver and game state belongs to the simulation thread. The worker only returns a result.
public sealed class DecisionDriver : IAsyncDisposable
{
    private Task<DecisionAttempt>? _pending;
    private CancellationTokenSource? _requestCancellation;
    private IDecisionClient? _client;
    private int _generation, _requestGeneration, _snapshotTick, _nextRequestTick;
    private readonly TimeSpan _timeout;
    private readonly int _maxAgeTicks;

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

    public void Switch(Game game, string provider, string model, IDecisionClient? client, string? error = null)
    {
        _generation++;
        _requestCancellation?.Cancel();
        game.Apply(GameAction.Stop);
        Provider = provider;
        Model = model;
        _client = client;
        Failed = error is not null;
        LastAttempt = null;
        Status = error ?? (client is null ? "Manual controls" : "Ready");
        _nextRequestTick = game.Tick;
    }

    public void Update(Game game)
    {
        if (_pending is { IsCompleted: false } && _requestGeneration == _generation &&
            _requestCancellation!.IsCancellationRequested && !Failed)
        {
            Failed = true;
            Status = "Request timed out; waiting for cancellation.";
            game.Apply(GameAction.Stop);
        }
        if (_pending is { IsCompleted: true })
        {
            DecisionAttempt attempt = _pending.GetAwaiter().GetResult();
            bool cancelled = _requestCancellation!.IsCancellationRequested;
            _pending = null;
            _requestCancellation.Dispose();
            _requestCancellation = null;
            if (_requestGeneration == _generation)
            {
                LastAttempt = attempt;
                if (cancelled || attempt.Error is not null)
                {
                    Failed = true;
                    Status = cancelled ? "Request timed out." : attempt.Error!;
                    game.Apply(GameAction.Stop);
                }
                else if (game.Tick - _snapshotTick > _maxAgeTicks || game.Finished)
                {
                    Rejected++;
                    Status = "Stale decision discarded";
                }
                else if (!IsLegal(attempt.Decision!, game, out GameAction action))
                {
                    Rejected++;
                    Status = "Illegal decision discarded";
                }
                else
                {
                    game.Apply(action);
                    Accepted++;
                    Status = $"Applied {attempt.Decision!.Choice}";
                }
            }
            else
            {
                Rejected++;
                Status = Failed ? Status : "Old provider decision discarded";
            }
        }
        if (_pending is not null || _client is null || Failed || game.Finished || game.Tick < _nextRequestTick)
            return;
        _snapshotTick = game.Tick;
        _requestGeneration = _generation;
        _nextRequestTick = game.Tick + 6;
        _requestCancellation = new CancellationTokenSource(_timeout);
        IDecisionClient client = _client;
        string snapshot = game.Snapshot();
        CancellationToken token = _requestCancellation.Token;
        _pending = Task.Run(() => DecisionCalls.ChooseAsync(client, snapshot, token));
    }

    public static bool IsLegal(ChoiceDecision decision, Game game, out GameAction action)
    {
        if (!Actions.TryMap(decision.Choice, out action)) return false;
        if (decision.Probabilities.Count != Actions.Options.Count ||
            Actions.Options.Keys.Any(key => !decision.Probabilities.ContainsKey(key)) ||
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
        if (_pending is not null) await _pending;
        _requestCancellation?.Dispose();
    }
}
