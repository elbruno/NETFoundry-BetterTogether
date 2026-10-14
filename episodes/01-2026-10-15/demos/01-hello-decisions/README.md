# Hello Decisions

One synthetic message, three explicit labels, one typed native decision. This sample uses **Microsoft-Decision-1** in an existing Microsoft Foundry deployment. `ElBruno.AI.Decisions.Foundry` is a **community SDK, not an official Microsoft SDK**.

## Read in this order

1. `Program.cs`: A selects the example/configuration, B makes one call, C prints the result.
2. `HelloDecision.cs`: the input examples, allowed labels, SDK call and validation.
3. `NativeDecision.cs`: the same Choice task using `HttpClient`, `System.Text.Json` and `Azure.Identity`.
4. `tests\ProtocolTests.cs`: synthetic HTTP tests, no cloud access.

Every C# source file starts with a Goal/A/B/C teaching header. This demo does not generate text, route tickets, or fall back to chat completions.

## Prerequisites and versions

- .NET SDK **10.0.401**, pinned in `global.json` with latest-patch roll-forward.
- `ElBruno.AI.Decisions.Foundry` **0.6.2**, configuration packages **10.0.0**. The package lock pins transitive dependencies including Azure.Identity **1.17.0**.
- Existing Microsoft-Decision-1 deployment and permission to invoke it. No resource creation is included.
- Azure CLI, `az login`, and the correct tenant/subscription for Microsoft Entra authentication. Both modes use `AzureCliCredential`, not an interactive credential chain.

## Configure before sharing your screen

From this directory, the convenient alternative is the episode's [setup helper](../../Setup-UserSecrets.ps1):

```powershell
& ..\..\Setup-UserSecrets.ps1
```

It configures each runnable demo's separate user-secret store. This project's `UserSecretsId` is `netfoundry-episode01-hello-decisions`. Never paste real settings into source files or recordings.

Required external keys:

| Key | Meaning |
| --- | --- |
| `Foundry:Endpoint` | HTTPS resource root, or exact `/providers/microsoft/v1/systemone` invocation URL. Not a project URL. |
| `Foundry:Model` | Your **deployment name**, which can differ from the catalog name. |
| `Foundry:ApiKey` | Optional key. Omit it for Azure CLI identity. A nonempty key takes precedence. |

Environment variables `Foundry__Endpoint`, `Foundry__Model`, `Foundry__ApiKey` override user secrets. For a session-only identity setup, enter these privately:

```powershell
$env:Foundry__Endpoint = Read-Host 'Foundry resource endpoint'
$env:Foundry__Model = Read-Host 'Decision deployment name'
$env:Foundry__ApiKey = '' # Ignore a stored key and use Azure CLI identity.
az login
```

Do not run a token-printing command or list user secrets on stream.

## Restore, build, test and run

Run all commands from this directory so `global.json` selects the tested SDK.

```powershell
dotnet restore .\tests --locked-mode
dotnet build .\tests -c Release --no-restore
dotnet test .\tests -c Release --no-build --no-restore
dotnet run -c Release --no-build -- --help
dotnet run -c Release --no-build -- --mode sdk --example billing
dotnet run -c Release --no-build -- --mode rest --example billing --inspect
dotnet run -c Release --no-build -- --example technical
dotnet run -c Release --no-build -- --example ambiguous
```

Each run makes **one request in one mode**, never an implicit SDK/REST comparison. The billing input is "I was charged twice for my subscription." The other inputs demonstrate technical failure and ambiguous upgrade/payment intent. Outputs are nondeterministic; no exact label, probability or latency is promised.

Output shape:

```text
Mode: sdk | Input: <synthetic message>
Choice: <billing|technical_support|sales>
  P(billing): <0..1>
  P(technical_support): <0..1>
  P(sales): <0..1>
Provider confidence (separate signal): <0..1>
Request duration: <measured milliseconds> ms | Calls: 1
```

Selected-label probability and provider confidence are **different signals**, neither a guarantee of correctness. There is no generated rationale.

## Native contract and SDK mapping

Verified against [Microsoft's official decision API guide](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision), including the `choice` `criteria` object and native provider route. See also the [community SDK implementation](https://github.com/elbruno/ElBruno.AI.Decisions/blob/main/src/ElBruno.AI.Decisions.Foundry/FoundryDecisionClient.cs). The tests verify the installed **0.6.2** package against the same named-question contract.

| SDK argument/result | Native JSON |
| --- | --- |
| Configured deployment | `model` |
| `input` | `state` |
| `instructions` | `questions.decision.instructions` |
| Options dictionary | `questions.decision.criteria`, with `type: "choice"` |
| `ChoiceDecision.Choice` | `answers.decision.choice` |
| `Probabilities` / `Confidence` | `answers.decision.probabilities` / `.confidence` |

`--inspect` prints a sanitized request and response **shape**, with the deployment replaced and no URL or authentication headers. It still makes the selected live request. The actual response is validated and displayed as typed fields, not dumped wholesale. REST uses `api-key` or a bearer token for `https://cognitiveservices.azure.com/.default`.

## Failure handling and tests

### Validation readiness

On October 14, 2026, bounded live checks succeeded for `--mode sdk --example billing` and `--mode rest --example billing`, one request each. Both used process-only external endpoint/deployment configuration and the existing Azure CLI identity. No resource was created. The technical and ambiguous examples have not been live-verified. Physical console rehearsal remains outstanding.

Both transports have a 30-second request deadline, cancellation via Ctrl+C, no retries and no fake fallback. The process has an additional 35-second deadline. Invalid configuration, authentication/transport failures, malformed answers, unknown labels and invalid distributions produce a sanitized error and exit **1**. Success/help exit **0**. Raw exception messages, response error bodies, endpoints and credentials are not printed.

The tests intercept HTTP in memory and verify serialization, SDK/REST parsing, Azure identity scope, authentication failure, cancellation, unsafe configuration and malformed responses. They require no credentials and incur no Foundry charge. Automated execution is not a claim of physical console rehearsal.

If a run fails, privately check the deployment name, resource-root URL, CLI tenant and invocation permissions; inspect `az account show` only off stream. A 401/403 requires authentication/authorization repair, not switching to a fake success. For 429 or a timeout, wait and manually rerun only if you accept another possible charge.

## Cost and cleanup

Live calls consume deployment quota and may incur Azure inference charges, including a request that times out locally after reaching the service. Review your deployment's pricing before running the examples repeatedly. Tests and `--help` make no network calls.

This demo creates no infrastructure. Do not delete a shared deployment. Clear only this demo's local configuration and build artifacts when desired:

```powershell
dotnet user-secrets clear --project .\HelloDecisions.csproj
Remove-Item Env:Foundry__Endpoint, Env:Foundry__Model, Env:Foundry__ApiKey -ErrorAction SilentlyContinue
Remove-Item .\bin, .\obj, .\tests\bin, .\tests\obj -Recurse -Force -ErrorAction SilentlyContinue
```
