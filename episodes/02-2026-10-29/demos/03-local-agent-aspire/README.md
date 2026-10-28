# Demo 03: Inspect local agent GenAI traces with Aspire

This is Demo 02's local support-routing agent with observability added. It has
its own independently runnable source and tests, plus a C# Aspire AppHost.
It uses the same synthetic ticket, question, four labels, native decision
model, Azure CLI credential, and JSON result. It does not add a chat model,
HTTP server, or cloud resource.

```text
Aspire AppHost -> local console -> instrumented AIAgent -> native decision model
                                     |                       |
                                     +------ OTLP spans -----+
                                               |
                                       Aspire dashboard
```

## Tested versions and prerequisites

| Component | Version |
| --- | --- |
| .NET SDK | 10.0.403 |
| Aspire CLI and AppHost SDK | 13.6.1 |
| Microsoft Agent Framework | 1.22.0 |
| Azure.Identity | 1.17.0 |
| ElBruno.AI.Decisions / ElBruno.AI.Decisions.Foundry | 0.6.3 |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.17.0 |

Tested on Windows with PowerShell. Install the .NET SDK and Aspire CLI
separately. A container runtime is not required: this AppHost only runs a
console project and the local dashboard. Use an existing Foundry
decision-model resource and deployment, with Azure CLI signed in as an
identity authorized for native inference. Do not use an API key.

The console and test lock files pin their dependency graphs. The file-based
`apphost.cs` pins its AppHost SDK. No code is referenced from another demo.

## Run

From this demo directory:

```powershell
dotnet restore .\tests\LocalRoutingAgent.Tests --locked-mode
dotnet test .\tests\LocalRoutingAgent.Tests -c Release --no-restore
dotnet build .\src\LocalRoutingAgent -c Release --no-restore

$env:Decision__Endpoint = 'https://<resource>.services.ai.azure.com/'
$env:Decision__Deployment = '<decision-deployment>'
aspire start --isolated --non-interactive
aspire wait routing-agent --status down --non-interactive
aspire logs routing-agent --non-interactive
```

Use the resource-root URL, not a project URL. The options-only decision client
owns its HTTP transport. The console makes **one** real model call, prints the
same typed JSON as Demo 02, flushes telemetry, and exits. `Finished` with exit
code `0` is expected; `down` alone can also mean failure. Check the resource
state in the dashboard and inspect its console output. The AppHost and
dashboard stay running so you can inspect the completed trace.

Open the dashboard login URL printed by `aspire start`. Treat that URL and
OTLP authentication headers as local secrets. Do not commit or share them.

## Inspect the GenAI trace

Open **Traces**, select `routing-agent`, then open the completed trace:

1. The Framework's `OpenTelemetryAgent` emits an `invoke_agent` parent span.
2. `DecisionTelemetry.cs` emits a native-model child span named
   `decision <deployment>`, using `ActivityKind.Client`.
3. Inspect `gen_ai.operation.name`, `gen_ai.provider.name`,
   `gen_ai.request.model`, duration, and `gen_ai.usage.input_tokens` /
   `gen_ai.usage.output_tokens` when the provider reports them.

The native decision client is not an `IChatClient`, so subscribing only to chat
instrumentation would miss this model call. The `decision` operation is an
application-defined GenAI operation, not a claim that SystemOne is chat
completion. No second inference call is made for tracing.

You can inspect the actual dashboard data from the same directory:

```powershell
aspire otel traces routing-agent --format Json --non-interactive
aspire otel spans routing-agent --format Json --non-interactive
```

On October 28, a real local request returned `billing`. The dashboard received
the parent and child in one trace, with 121 input tokens and 1 output token
on the model span. Those are observed values, not deterministic expectations.
Missing total, reasoning, and cache counters are not invented or set to zero.
Confidence is not a correctness guarantee.

Both `EnableSensitiveData = false` and message-content capture disabled are
explicit. The trace pipeline subscribes only to the agent and custom decision
sources, not HTTP/authentication instrumentation. It does not record tickets,
response bodies, model-resource URLs, credentials, or full exception messages
on the native span. Use synthetic data anyway. The resource environment panel
contains real configuration; do not display or expand it during recording.

## Reading order and offline checks

Read `apphost.cs`, `src\LocalRoutingAgent\Program.cs`, then
`SupportRoutingAgent.cs` and `DecisionTelemetry.cs`.
The five offline tests use labeled synthetic decisions, not Azure. They check
parent/child trace IDs, preserved structured results, absent versus zero usage,
input limits, cancellation, and provider errors. A provider failure is not
converted into a successful `manual-review` route.

Change the `ticket` constant or its commented alternative in `Program.cs` to
try a different short synthetic ticket. From the dashboard, start the finished
resource again; use **Rebuild** if you changed its source. Each start makes a
new billable model call.

## Costs and cleanup

The local AppHost/dashboard create no Azure infrastructure. Native inference
still incurs Foundry charges. This sample exports to the local dashboard, not
Application Insights or another hosted telemetry service.

Stop only this demo's AppHost, from this demo directory:

```powershell
aspire stop --non-interactive
Remove-Item Env:Decision__Endpoint, Env:Decision__Deployment -ErrorAction SilentlyContinue
```

This removes no shared Azure resources. Dashboard telemetry is local and is
not a durable record; do not depend on it surviving shutdown.

The custom agent pattern is adapted from Microsoft Foundry samples.
See [source attribution](THIRD-PARTY-NOTICES.md).
Continue to [Demo 04: Run the agent as a Foundry Hosted Agent](../04-foundry-hosted-agent/README.md).
