# Optional: answer-quality review

Review **three prewritten synthetic answers** against one supplied reference policy. No generative model is involved. Score measures rubric quality, Assess estimates whether the answer's claims/actions are supported, and application policy emits `ready_for_review` or `needs_revision`. Nothing is automatically approved or published.

Uses `ElBruno.AI.Decisions.Foundry` **0.6.2**, a **community SDK, not an official Microsoft SDK**, with an existing Microsoft-Decision-1 deployment.

## Read in this order

1. `answers.v1.json`: synthetic customer question, reference policy, supported/incomplete/unsupported answers. Expected statuses are evaluation metadata.
2. `Program.cs`: A selects input/thresholds, B asks questions, C prints review-only outcomes.
3. `AnswerReviewer.cs`: rubric, support proposition and two separate calls.
4. `ReviewPolicy.cs`: both signals must pass explicit application thresholds.
5. `tests\ReviewTests.cs`: deterministic fake outputs and synthetic HTTP responses.

Each C# file includes a Goal/A/B/C teaching header and conceptual landmarks.

## Prerequisites and safe configuration

- .NET SDK **10.0.401**, pinned with latest-patch roll-forward.
- Foundry community package **0.6.2**, configuration packages **10.0.0** and committed dependency locks. Azure.Identity **1.17.0** is a locked transitive dependency.
- Existing Microsoft-Decision-1 deployment with invocation permission. No cloud resources are created.
- Azure CLI and `az login` for the default **AzureCliCredential** path.
- Required external `Foundry:Endpoint` and `Foundry:Model`. Model means **deployment name**, not necessarily the catalog name. Endpoint means HTTPS resource root or exact `/providers/microsoft/v1/systemone` URL, not a project URL.
- Optional external `Foundry:ApiKey` takes precedence over identity.

Privately configure with the episode's [setup helper](../../Setup-UserSecrets.ps1):

```powershell
& ..\..\Setup-UserSecrets.ps1
```

This demo's separate `UserSecretsId` is `netfoundry-episode01-answer-review`. The environment keys `Foundry__Endpoint`, `Foundry__Model`, `Foundry__ApiKey` override that store. For a session-only identity alternative:

```powershell
$env:Foundry__Endpoint = Read-Host 'Foundry resource endpoint'
$env:Foundry__Model = Read-Host 'Decision deployment name'
$env:Foundry__ApiKey = '' # Override a stored key and use CLI identity.
az login
```

Configure before sharing your screen. Never list user secrets, print tokens or paste real deployment details into source.

## Exact commands

From this directory:

```powershell
dotnet restore .\tests --locked-mode
dotnet build .\tests -c Release --no-restore
dotnet test .\tests -c Release --no-build --no-restore
dotnet run -c Release --no-build -- --help
dotnet run -c Release --no-build --
dotnet run -c Release --no-build -- --answer incomplete --details
dotnet run -c Release --no-build -- --answer unsupported --inspect
dotnet run -c Release --no-build -- --all
dotnet run -c Release --no-build -- --answer supported --minimum-score 2.5 --minimum-support 0.85
```

Default: supported example, **2 calls**. `--all`: three examples, **at most 6 calls**. `--answer` and `--all` are mutually exclusive. The shared SDK makes a separate request for Score and Assess. Per-call deadline is 30 seconds, total deadline is 120 seconds, Ctrl+C cancels, and there are no automatic retries.

`--inspect` shows sanitized request/response shapes and still makes live calls. Expected statuses stay outside the model input; resolved URLs, deployment names and headers are never printed. `--details` adds the quality distribution.

Expected output shape:

```text
supported: quality=<0..3> | P(supported)=<0..1>
  <ready_for_review|needs_revision>: <application policy reason>
Calls attempted: <count>/6 | Failures: <count>
Fixture-status agreement: <matches>/<selected examples>. Not a benchmark.
Model-assisted review only: no answer was approved or published.
```

The written rubric runs from unsupported/unsafe at index 0 through complete/supported at index 3. Score is a **probability-weighted zero-based position**, potentially fractional, not a rounded grade. Support estimates a separate proposition. The pinned SDK does not expose provider confidence for Score/Assess; do not relabel their values as confidence.

Default application policy requires **quality >= 2.5 AND support >= 0.85**. Equality passes. Invalid/missing signals and failures cannot become review-ready. Both thresholds are configurable, explicitly **uncalibrated teaching examples**, not guarantees. An incomplete answer may be supported yet still require revision. Output and latency vary; expected fixture statuses are not assertions about exact live outputs.

This is **model-assisted review, not proof of truth, policy compliance or a security boundary**. Even `ready_for_review` requires a person. There is no approval/publish API. See [Microsoft's official native decision guide](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision) for score/noul semantics.

## Tests, failures and troubleshooting

### Validation readiness

On October 14, 2026, a bounded live check succeeded for `--answer supported`, making two requests: Score and Assess. It used process-only external endpoint/deployment configuration and the existing Azure CLI identity. No resource was created. The incomplete and unsupported examples and the all-examples run have not been live-verified. Physical console rehearsal remains outstanding.

Tests use synthetic HTTP for native score/noul serialization and parsing, fake typed outputs for policy boundaries, malformed-score rejection, call caps, cancellation and explicit failure handling. They verify fixture expectations never enter model state. No cloud credentials or chargeable requests are needed. Automated validation is not a physical console rehearsal.

Configuration/argument errors, invalid results and request failures exit **1**. Failures are visible as `needs_revision` with a sanitized exception type, never a fake success. Success/help exit **0**. Numerical disagreement with fixture expectations alone is not a transport failure.

On failure, privately verify deployment name, native resource-root URL, CLI tenant and invocation permission. 401/403 indicates authentication/authorization issues; 429/timeouts may require waiting before an explicitly chosen retry. Error bodies and raw exception messages are suppressed because they can contain deployment/identity details.

## Cost and cleanup

Live calls use existing Azure inference capacity and may incur charges. `--all` triples the default request count. Locally timed-out requests may still be billed; check your deployment's pricing. Tests and help make no Azure calls.

No infrastructure or published content is created. Do not delete a shared deployment. Optional local cleanup:

```powershell
dotnet user-secrets clear --project .\AnswerReview.csproj
Remove-Item Env:Foundry__Endpoint, Env:Foundry__Model, Env:Foundry__ApiKey -ErrorAction SilentlyContinue
Remove-Item .\bin, .\obj, .\tests\bin, .\tests\obj -Recurse -Force -ErrorAction SilentlyContinue
```
