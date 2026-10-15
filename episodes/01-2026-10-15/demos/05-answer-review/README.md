# Model-assisted answer review

One straight-line `Program.cs`: **A** creates the client, **B** asks Score and Assess about one prewritten synthetic answer and reference policy, **C** prints the signals. Two commented alternate answers show incomplete and unsupported guidance. Neither method generates an answer.

## Prerequisites and configuration

- .NET SDK **10.0.401**, with latest-patch roll-forward in `global.json`.
- `ElBruno.AI.Decisions.Foundry` **0.6.3**, a **community SDK, not an official Microsoft SDK**; configuration packages **10.0.0**. Versions are locked.
- An existing Microsoft-Decision-1 deployment and permission to invoke it. No resources are provisioned.
- Azure CLI with `az login`, or an externally supplied API key.

From this directory, use the episode's [setup helper](../../Setup-UserSecrets.ps1):

```powershell
& ..\..\Setup-UserSecrets.ps1
```

The unchanged project-specific `UserSecretsId` is `netfoundry-episode01-answer-review`. Alternatively, set process environment variables, which override user secrets:

```powershell
$env:Foundry__Endpoint = Read-Host 'Foundry resource endpoint'
$env:Foundry__Model = Read-Host 'Decision deployment name'
$env:Foundry__ApiKey = '' # Use the existing Azure CLI identity.
az login
```

Use an HTTPS Foundry resource endpoint, not a project URL. `Foundry:Model` is the deployment name. Set `Foundry:ApiKey` privately only if using key authentication. Never display secrets or actual configuration during a recording.

## Run

Run from this directory:

```powershell
dotnet restore .\AnswerReview.csproj --locked-mode
dotnet build .\AnswerReview.csproj -c Release --no-restore
dotnet run --project .\AnswerReview.csproj -c Release --no-build --no-restore
```

Each run makes **two separate HTTP requests**. Edit `answer` to use a commented alternative, then rebuild. There are no CLI arguments, fixtures, custom helpers or offline test project. The reference, question and answer are fixed synthetic text in the source.

## Interpret the output

SDK 0.6.3 exposes usage for both Score and Assess. Each call prints its reported
input/output/total counters. Live Foundry populated input/output and omitted
total; absent counters remain **not reported**, never zero or estimated.

Score prints a probability-weighted zero-based quality index **0..3** and each rubric-level probability. It can be fractional and is not a rounded grade. Assess estimates the probability **0..1** that the answer's claims and actions are supported by the supplied reference. SDK 0.6.3 does not expose provider confidence for these methods.

The status expression uses quality **>= 2.5** and support **>= 0.85**. These thresholds are **uncalibrated teaching policy**, not validated approval criteria or a safety guarantee. Even `Ready for human review` still requires a person. Model signals do not establish truth, policy compliance or injection immunity. No answer is approved or published; values vary between runs.

Native question semantics: [Microsoft-Decision-1 guide](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision).

## Errors, costs and cleanup

Missing settings, failed authentication and HTTP failures surface as normal unhandled exceptions; there is no fallback. Check the deployment name, endpoint, CLI tenant and permissions for 401/403. For quota limits, 429 or timeouts, wait and rerun manually only if additional charges are acceptable. The SDK has a default per-request timeout; this minimal app adds no retry or total-deadline logic. Keep raw errors off a shared screen because they can contain configuration.

Live requests consume quota and may incur Azure inference charges, including requests that time out locally. Consult the existing deployment's pricing. No resources are created; do not delete shared infrastructure.

```powershell
dotnet user-secrets clear --project .\AnswerReview.csproj
Remove-Item Env:Foundry__Endpoint, Env:Foundry__Model, Env:Foundry__ApiKey -ErrorAction SilentlyContinue
Remove-Item .\bin, .\obj -Recurse -Force -ErrorAction SilentlyContinue
```
