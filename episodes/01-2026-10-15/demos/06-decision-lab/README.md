# Decision Lab: parallel comparisons in Blazor and Aspire

Choose predefined support questions, set a repeat count, and compare a Foundry
generative model with **Microsoft-Decision-1** in a live Blazor page. This is the
web extension of [console demo 03](../03-compare-decisions/README.md).

The generative path uses **Microsoft.Extensions.AI.IChatClient**, the OpenAI
adapter and a strict JSON label schema. The native decision path uses
**ElBruno.AI.Decisions.Foundry**. It does not bypass either SDK with raw HTTP.
ElBruno.AI.Decisions is a community SDK, not an official Microsoft SDK.

## Important token-usage limitation

**Decision SDK 0.6.2 does not expose token usage.** Its page counters display
**not exposed**, not zero or estimated counts. GPT's input, output, and total
counters come from `ChatResponse.Usage`. Missing GPT fields display not reported.
The requested SDK enhancement is tracked in
[elbruno/ElBruno.AI.Decisions#4](https://github.com/elbruno/ElBruno.AI.Decisions/issues/4).
Until the library exposes usage, a complete SDK-only token comparison is not
available. The native service's ability to return usage is distinct from what
this SDK result exposes.

## Prerequisites

- .NET SDK **10.0.401**, targeting .NET 10, latest-patch roll-forward.
- Aspire CLI **13.6.1**; AppHost SDK **13.6.1**.
- Existing **gpt-6.1-sol** (or a compatible chat-completions model with strict
  JSON schema support) and **Microsoft-Decision-1** deployments on one Foundry
  resource. No Azure resources or model deployments are created by this sample.
- Authorized Azure CLI identity, or an externally configured optional API key.
  Run `az login` off stream if needed.
- A local browser. Docker is not needed: the AppHost starts one .NET web service
  and the local Aspire dashboard, with no containers or databases.

Pinned app packages: **Microsoft.Extensions.AI 10.10.0**,
**Microsoft.Extensions.AI.OpenAI 10.10.0**, **Azure.AI.OpenAI 2.9.0-beta.1**,
**Azure.Identity 1.17.0**, **ElBruno.AI.Decisions.Foundry 0.6.2**.
ServiceDefaults uses **OpenTelemetry 1.17.0**. Each project has a dependency lock.
The console example's Responses adapter and this web example's chat-completions
adapter both implement MEAI; timings between the two apps are not a controlled
API/model benchmark.

## Configure and run

From **this directory**, before screen sharing:

```powershell
pwsh -File ..\..\Setup-UserSecrets.ps1
dotnet restore DecisionLab.sln --locked-mode
aspire start --non-interactive
aspire wait webfrontend --non-interactive
aspire describe --non-interactive
```

Use the application and dashboard URLs reported by Aspire. Ports can vary.
Never paste dashboard login tokens into documentation or source control.
To isolate ports from other local Aspire apps, use `aspire start --isolated
--non-interactive` instead. Use Aspire, not `dotnet run` on the AppHost.

The helper configures only **DecisionLab.Web**, not the orchestration/class-library
projects. User-secrets ID: `netfoundry-episode01-decision-lab`. Required keys:
`Foundry:Endpoint` (HTTPS resource root), `Foundry:Model` (decision deployment),
`Foundry:GenerativeModel` (generative deployment), optional `Foundry:ApiKey`.
`Foundry__*` environment variables override user secrets. Identity mode removes
a stale stored key. User secrets are unencrypted local development storage.
Model credentials remain server-side, not in the browser or committed settings.

The app is a **local development demo without user authentication**, not a
production-ready public inference service. Keep its application/dashboard on
loopback. Do not publish or expose it to a network without separate authentication,
authorization, per-user quotas, and operational controls.

## Run a comparison

1. Select one or more predefined questions. Start with duplicate charge.
2. Choose **1-20 repetitions per selected question**, default 1.
3. Review the displayed planned request count, then click **Start comparison**.
4. Watch both rows update as their responses arrive, not only at the end.
5. Click **Stop run** to cancel in-flight requests and prevent the next pair.

Each pair starts chat and decision calls together with `Task.WhenAll`. The next
pair waits for both outcomes, keeping at most **two model requests in flight per
browser circuit**. Four questions times 20 repetitions means at most 80 pairs,
160 planned requests per run. There is a short cancellable inter-pair delay,
90-second request deadline, and no automatic inference retries. Different tabs
have independent state and can make additional calls.

New runs clear previous rows/totals. A disconnect or page disposal cancels the
current run. Reconnecting does not resume it automatically. State is in memory;
there is no database, export, synthetic fallback, or model call on page load.
Provider errors and cancellations have their own visible statuses, never a
success-shaped substitute. A successful peer result remains visible if the
other provider fails.

Cards show successful/failed/cancelled counts, successful mean client-observed
latency, usage coverage, and cumulative reported usage from successful responses.
Failed, refused, invalid, or cancelled responses are excluded from usage totals
and may still have incurred charges. Reasoning tokens, if used, are already part
of output usage; this app does not add them again. Latency includes client/auth
work inside the SDK call, not just service inference.

**This is an illustration, not a benchmark or billing ledger.** Tokenizers,
model defaults, schema serialization, cold starts, network routing and reasoning
can differ. There is no dollar/savings estimate, calibrated accuracy claim,
production threshold, or claim that Microsoft-Decision-1 runs locally.

## Aspire GenAI walkthrough

1. Open the dashboard URL returned by Aspire. Run one comparison in the app.
2. In **Traces**, find the `Home.Start` event and expand **comparison.pair**.
3. The sibling `chat.call` and `decision.call` spans overlap in time.
4. Open **GenAI details** on `chat <deployment>`: inspect model, input/output
   token usage, finish status and duration.
5. Open **GenAI details** on `decision <deployment>`: inspect its operation and
   duration. There are no token tags because the SDK does not expose counters.
   This is an explicit app-authored GenAI span around `ChooseAsync`, not built-in
   instrumentation claimed to come from the decision library.

MEAI emits real chat spans/metrics. The app's ActivitySource is
`DecisionLab.Models`; ServiceDefaults subscribes to its spans/meter and exports
OTLP to the Aspire-managed dashboard. Both model outcomes share the same trace;
the table shows its short ID to correlate with the dashboard.

**Prompt/response body recording is disabled**, including the Aspire child
environment setting. The GenAI visualizer deliberately says no message content
was recorded. An exporter processor replaces external host names with `foundry`
and removes full outgoing request URLs, retaining actual model usage attributes.
Use audience-safe deployment names. Dashboard resource environment/configuration
panels and exported diagnostics may contain credentials; do not share those
panels, raw startup logs, or telemetry exports during a livestream.

CLI inspection from this directory:

```powershell
aspire otel traces webfrontend --non-interactive
aspire otel spans webfrontend --search "chat" --non-interactive
aspire otel spans webfrontend --search "decision" --non-interactive
```

## Code reading order

- `DecisionLab.Web/DecisionQuestion.cs`: the shared task, labels, predefined inputs.
- `DecisionLab.Web/ModelComparison.cs`: client setup, MEAI and native SDK calls,
  actual available usage, GenAI spans, explicit failure statuses.
- `DecisionLab.Web/ComparisonRun.cs`: bounded repeats, parallel pair scheduling,
  cancellation and disconnect handling.
- `DecisionLab.Web/Components/Pages/Home.razor`: live results and cumulative UI.
- `DecisionLab.AppHost/AppHost.cs`: one web resource and privacy setting.
- `DecisionLab.ServiceDefaults/Extensions.cs`: OTel subscription/export.

This richer app necessarily has UI/run/telemetry plumbing. It does not replace
the short console samples as the first teaching step.

## Validation, costs, and cleanup

```powershell
dotnet test .\tests\DecisionLab.Tests.csproj -c Release
```

Tests use explicit fake SDK clients, no cloud calls. They cover parallel start,
repeat counts, cancellation/disconnect, invalid bounds, overlapping runs, provider
failure, invalid labels/JSON, incomplete chat responses, reported zero versus
missing usage, and endpoint redaction. A live browser rehearsal verified two
questions repeated twice, progressive totals, Stop cancellation, and Aspire
GenAI details for both providers. Results are not expected fixed probabilities.

Every started provider request can consume quota/incur charges, even if cancelled.
Stop in the page first, then stop the app:

```powershell
aspire stop --non-interactive
dotnet user-secrets clear --project .\DecisionLab.Web\DecisionLab.Web.csproj
```

Only clear that project's settings if no longer needed. Remove any `Foundry__*`
overrides separately. No cloud resources need cleanup; never delete shared
deployments. Diagnose configuration/auth errors off stream.

[Back to the episode](../../README.md)
