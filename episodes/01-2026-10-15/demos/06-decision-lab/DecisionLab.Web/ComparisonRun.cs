/*
 * Goal: Run bounded parallel model pairs with visible progress and cancellation.
 * A. Snapshot the selected questions and repeat count.
 * B. Start both models together; await a pair before starting the next pair.
 * C. Keep cumulative usage and stop on user cancellation or circuit disconnect.
 */
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace DecisionLab.Web;

public sealed class ComparisonRun(IModelComparison models) : IDisposable
{
    public const int MaxRepetitions = 100;
    private CancellationTokenSource? cancellation;
    public bool IsRunning { get; private set; }
    public List<ComparisonPair> Results { get; } = [];
    public int PlannedPairs { get; private set; }
    public int Repetitions { get; private set; }
    public event Action? Changed;

    public async Task StartAsync(IReadOnlyList<DecisionQuestion> questions, int repeats)
    {
        if (IsRunning) throw new InvalidOperationException("A run is already active.");
        if (questions.Count == 0 || repeats is < 1 or > MaxRepetitions)
            throw new ArgumentException($"Choose at least one question and 1-{MaxRepetitions} repetitions.");
        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        Results.Clear();
        PlannedPairs = questions.Count * repeats;
        Repetitions = repeats;
        IsRunning = true;
        Changed?.Invoke();
        try
        {
            for (int repeat = 1; repeat <= repeats && !token.IsCancellationRequested; repeat++)
            {
                foreach (var question in questions)
                {
                    if (token.IsCancellationRequested) break;
                    var pair = new ComparisonPair(Results.Count + 1, question);
                    Results.Add(pair);
                    Changed?.Invoke();
                    using var trace = ModelComparison.Traces.StartActivity("comparison.pair");
                    trace?.SetTag("comparison.pair", pair.Number);
                    var chat = ObserveAsync(models.ChatAsync(question, token), result => pair.Chat = result);
                    var decision = ObserveAsync(models.DecideAsync(question, token), result => pair.Decision = result);
                    await Task.WhenAll(chat, decision);
                    // A small cancellable gap prevents tight repeated traffic after instant failures.
                    if (!token.IsCancellationRequested)
                        await Task.Delay(250, token);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            IsRunning = false;
            Changed?.Invoke();
        }
    }

    private async Task ObserveAsync(Task<ModelResult> call, Action<ModelResult> update)
    {
        update(await call);
        Changed?.Invoke();
    }

    public void Stop() => cancellation?.Cancel();
    public void Dispose()
    {
        Stop();
        cancellation?.Dispose();
    }
}

public sealed class ComparisonPair(int number, DecisionQuestion question)
{
    public int Number { get; } = number;
    public DecisionQuestion Question { get; } = question;
    public ModelResult? Chat { get; set; }
    public ModelResult? Decision { get; set; }
}

public sealed class RunCircuitHandler(ComparisonRun run) : CircuitHandler
{
    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken token)
    {
        run.Stop();
        return Task.CompletedTask;
    }
}
