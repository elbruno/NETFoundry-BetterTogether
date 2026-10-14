# Support inbox triage

Three native questions about one synthetic ticket: **Choice** suggests a queue, **Score** estimates urgency on an ordered rubric, and **Assess** estimates the probability of an account-access problem. Application code decides whether the displayed suggestion needs human review. Nothing connects to a support system.

The Foundry client is `ElBruno.AI.Decisions.Foundry` **0.6.2**, a **community SDK, not an official Microsoft SDK**. Its shared API makes **three separate HTTP calls**, not a batched request.

## Read in this order

1. `tickets.v1.json`: six synthetic cases with separate expected-label/review metadata.
2. `Program.cs`: A selects fixtures, B asks questions, C summarizes signals and agreement.
3. `Triage.cs`: allowed queue descriptions, ordered rubric, proposition and the three calls.
4. `DecisionPolicy.cs`: validation and application-owned review policy.
5. `tests\TriageTests.cs`: fake typed results and synthetic HTTP contracts.

Each C# file starts with a Goal/A/B/C header and conceptual landmarks.

## Prerequisites and configuration

- .NET SDK **10.0.401** with latest-patch roll-forward in `global.json`.
- Pinned community Foundry package **0.6.2**, configuration packages **10.0.0**, committed NuGet locks. Azure.Identity **1.17.0** is a locked transitive dependency.
- Existing Microsoft-Decision-1 deployment, permission to invoke it, and Azure CLI with `az login`. No resources are created.
- External `Foundry:Endpoint` and `Foundry:Model`. The model is a **deployment name**, not necessarily the catalog name. The endpoint must be an HTTPS resource root or the exact `/providers/microsoft/v1/systemone` URL, not a project URL.
- Optional `Foundry:ApiKey`, supplied externally. Without a nonempty key the SDK uses **AzureCliCredential**.

Configure privately with the episode's [setup helper](../../Setup-UserSecrets.ps1):

```powershell
& ..\..\Setup-UserSecrets.ps1
```

This project's unique `UserSecretsId` is `netfoundry-episode01-support-triage`. Do not assume another demo's secrets are inherited. Environment variables `Foundry__Endpoint`, `Foundry__Model`, `Foundry__ApiKey` override this store. For a session-only identity alternative:

```powershell
$env:Foundry__Endpoint = Read-Host 'Foundry resource endpoint'
$env:Foundry__Model = Read-Host 'Decision deployment name'
$env:Foundry__ApiKey = '' # Override a stored key and use CLI identity.
az login
```

Never list user secrets, print tokens or configure real deployment details during a recording.

## Exact commands

Run from this directory to use the pinned SDK:

```powershell
dotnet restore .\tests --locked-mode
dotnet build .\tests -c Release --no-restore
dotnet test .\tests -c Release --no-build --no-restore
dotnet run -c Release --no-build -- --help
dotnet run -c Release --no-build --
dotnet run -c Release --no-build -- --ticket account --details
dotnet run -c Release --no-build -- --ticket ambiguous --threshold 0.80 --inspect
dotnet run -c Release --no-build -- --ticket adversarial
dotnet run -c Release --no-build -- --all
```

Default: `billing`, **3 calls**. `--all` explicitly selects six fixtures, **at most 18 calls**. `--ticket` and `--all` are mutually exclusive. Each call has a 30-second deadline; a 120-second total deadline and Ctrl+C cancel remaining work. There are no automatic retries. Aborted all-fixtures runs may leave some tickets unevaluated.

`--inspect` prints sanitized native request/response shapes, not headers, URLs, deployment settings or expected labels. It still makes live calls. `--details` adds distributions; the default output is compact.

Expected output shape, not promised numerical results:

```text
billing: queue=<allowed label> | urgency=<0..3> | P(account access)=<0..1>
  <human_review|simulated_suggestion>: <application policy reason>
  P(selected label)=<0..1>; provider confidence=<0..1> (different signals)
Calls attempted: <count>/18 | Failures: <count>
Routing agreement on labeled selected tickets: <matches>/<denominator> ...
```

## Interpret signals and policy

The allowed queues are `billing`, `technical_support` (including account access), `sales`, `other`. Ticket text, including adversarial instructions, remains **data**. Returned labels cannot invoke tools.

The urgency rubric is low, normal, high, critical at zero-based indexes **0, 1, 2, 3**. `ScoreDecision.Score` is the **probability-weighted index**, potentially fractional. It is not automatically rounded into a severity label. `AssessmentDecision.Probability` estimates the account-access proposition. The pinned SDK exposes Choice provider confidence separately, but does **not** expose Score/Assess provider confidence.

`DecisionPolicy.Decide`:

1. Reject invalid/missing outputs, unknown labels and invalid distributions before accepting a suggestion.
2. Require human review if the **selected-label probability** is below `--threshold` (default **0.75**). Equality passes. Provider confidence is not substituted.
3. Require review for `other` and the explicitly marked ambiguous/adversarial fixtures regardless of probability.
4. Otherwise display `simulated_suggestion`. No external action occurs.

The fixture review flags represent known demonstration/evaluation cases, **not automatic detection of all ambiguous or adversarial content**. The threshold is uncalibrated teaching policy, not a production guarantee or injection defense. Even passing examples do not establish injection immunity.

Expected labels remain outside model inputs. Four fixtures have an expected queue; the ambiguous and adversarial cases are unlabeled and review-required. Agreement is reported with the selected labeled denominator and valid-evaluated count, without silently excluding failures. This tiny synthetic set is **not a benchmark**. Model outputs and latency vary.

Native question types and score interpretation follow [Microsoft's official guide](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision).

## Testing, failures and troubleshooting

### Validation readiness

On October 14, 2026, a bounded live check succeeded for `--ticket billing`, making three requests: Choice, Score and Assess. It used process-only external endpoint/deployment configuration and the existing Azure CLI identity. No resource was created. Other tickets and the all-fixtures run have not been live-verified. Physical console rehearsal remains outstanding.

Tests use fake `IDecisionClient` outputs and in-memory HTTP answers for all three question types. They cover threshold equality/boundaries, invalid/missing signals, unknown labels, explicit failures, request caps, cancellation, fixture metadata isolation and rubric serialization. They require no credentials and incur no charges. Automated validation is not physical console rehearsal.

Invalid arguments/configuration fail with exit **1**. Per-ticket failures print a sanitized exception type, mark human review and increment failure count. Any invalid result or request failure yields process exit **1**; success/help exit **0**. Model disagreement alone is not a transport failure. There is no silent fake or chat fallback.

Privately check deployment name, HTTPS resource endpoint, CLI tenant and invocation permissions for 401/403. For 429/timeouts, wait and manually rerun only if additional charges are acceptable. Raw diagnostics and error bodies are deliberately not printed because they can expose configuration.

## Cost and cleanup

Live calls can incur Azure inference charges and consume quota. The all-fixtures option multiplies calls sixfold; locally timed-out requests may still be billed. Review pricing for your existing deployment. Tests and `--help` never call Azure.

No resources or real tickets are created or modified. Do not delete shared infrastructure. Optional local cleanup:

```powershell
dotnet user-secrets clear --project .\SupportTriage.csproj
Remove-Item Env:Foundry__Endpoint, Env:Foundry__Model, Env:Foundry__ApiKey -ErrorAction SilentlyContinue
Remove-Item .\bin, .\obj, .\tests\bin, .\tests\obj -Recurse -Force -ErrorAction SilentlyContinue
```
