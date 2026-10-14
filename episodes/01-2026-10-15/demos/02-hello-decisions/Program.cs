/*
 * Goal: Walk through one native decision in either SDK or REST mode.
 * A. Select a synthetic example and load external configuration.
 * B. Measure one bounded request using the selected transport.
 * C. Display validated probabilities and confidence without exposing configuration.
 * Inputs: --mode sdk|rest, --example billing|technical|ambiguous, --inspect.
 * Output: A typed decision or a sanitized failure with a nonzero exit code.
 * Read next: HelloDecision.cs, then NativeDecision.cs.
 * Boundary: This is not an interactive console rehearsal or an automated router.
 */
using System.Diagnostics;
using System.Globalization;
using ElBruno.AI.Decisions;
using ElBruno.AI.Decisions.Foundry;
using HelloDecisions;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
try
{
    // A. Reject unknown arguments before loading any credentials.
    string mode = "sdk", example = "billing";
    bool inspect = false;
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--help":
                Console.WriteLine("Hello Decisions: --mode sdk|rest --example billing|technical|ambiguous --inspect\nOne request, 30-second deadline. SDK means community SDK, not official Microsoft SDK.");
                return 0;
            case "--mode" when i + 1 < args.Length: mode = args[++i]; break;
            case "--example" when i + 1 < args.Length: example = args[++i]; break;
            case "--inspect": inspect = true; break;
            default: throw new ArgumentException("Unknown or incomplete argument. Use --help.");
        }
    }
    if (mode is not ("sdk" or "rest")) throw new ArgumentException("Mode must be sdk or rest.");
    string input = HelloDecision.Input(example);
    var options = DemoConfiguration.Load();
    Console.WriteLine($"Mode: {mode} | Input: {input}");
    if (inspect)
    {
        Console.WriteLine("Sanitized request (deployment replaced; no URL or headers):");
        Console.WriteLine(NativeDecision.RequestBody("<deployment-name>", input));
        Console.WriteLine("Response shape: answers.decision = { type: choice, choice: <label>, probabilities: { <label>: <0..1> }, confidence: <0..1> }");
    }

    // B. Ctrl+C cancels token acquisition and the request. There is no retry or fallback.
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(35));
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    var timer = Stopwatch.StartNew();
    ChoiceDecision result;
    if (mode == "sdk")
    {
        using var client = new FoundryDecisionClient(options);
        result = await HelloDecision.AskAsync(client, input, cancellation.Token);
    }
    else
    {
        using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        { Timeout = Timeout.InfiniteTimeSpan };
        result = await NativeDecision.AskAsync(http, options, input, cancellation.Token);
    }
    timer.Stop();

    // C. Provider confidence is not the selected label's probability or calibrated correctness.
    Console.WriteLine($"Choice: {result.Choice}");
    foreach (var label in HelloDecision.Options.Keys)
        Console.WriteLine($"  P({label}): {result.Probabilities[label]:F4}");
    Console.WriteLine($"Provider confidence (separate signal): {result.Confidence:F4}");
    Console.WriteLine($"Request duration: {timer.Elapsed.TotalMilliseconds:F0} ms | Calls: 1");
    return 0;
}
catch (Exception error)
{
    // Exception messages can contain resolved URLs, response bodies or identity diagnostics.
    Console.Error.WriteLine($"Failed ({DemoErrors.Describe(error)}). Check arguments, external configuration, Azure CLI permissions, native response shape, and the 30-second request deadline. No fallback was used.");
    return 1;
}
