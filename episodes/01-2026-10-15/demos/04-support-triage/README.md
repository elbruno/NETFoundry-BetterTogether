# Support inbox triage

One straight-line `Program.cs`: **A** creates the client, **B** asks Choice, Score and Assess about a fixed synthetic ticket, **C** prints the signals. Two commented alternate tickets are ready to try. No support system is connected.

## Prerequisites and configuration

- .NET SDK **10.0.401**, with latest-patch roll-forward in `global.json`.
- `ElBruno.AI.Decisions.Foundry` **0.6.2**, a **community SDK, not an official Microsoft SDK**; configuration packages **10.0.0**. Versions are locked.
- An existing Microsoft-Decision-1 deployment and permission to invoke it. No resources are provisioned.
- Azure CLI with `az login`, or an externally supplied API key.

From this directory, use the episode's [setup helper](../../Setup-UserSecrets.ps1):

```powershell
& ..\..\Setup-UserSecrets.ps1
```

The unchanged project-specific `UserSecretsId` is `netfoundry-episode01-support-triage`. Alternatively, set process environment variables, which override user secrets:

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
dotnet restore .\SupportTriage.csproj --locked-mode
dotnet build .\SupportTriage.csproj -c Release --no-restore
dotnet run --project .\SupportTriage.csproj -c Release --no-build --no-restore
```

Each run makes **three separate HTTP requests**, one per method. Edit `prompt` to use a commented alternative, then rebuild. There are no CLI arguments, fixtures, custom helpers or offline test project.

## Interpret the output

Choice prints the suggested queue and probabilities for all labels. Provider confidence is a **separate signal**, not the selected-label probability or a correctness guarantee. Score is the probability-weighted zero-based urgency index **0..3**, potentially fractional, not a rounded severity label. Assess is the estimated probability **0..1** that the ticket concerns account access. SDK 0.6.2 does not expose Score/Assess provider confidence.

The displayed human-review status uses the selected-label probability threshold **0.75** and the `other` label. This is explicitly **uncalibrated teaching policy**, not a production routing rule, safety guarantee or ambiguity detector. Both branches require human review. Nothing is routed or changed; values vary between runs.

Native question semantics: [Microsoft-Decision-1 guide](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision).

## Errors, costs and cleanup

Missing settings, failed authentication and HTTP failures surface as normal unhandled exceptions; there is no fallback. Check the deployment name, endpoint, CLI tenant and permissions for 401/403. For quota limits, 429 or timeouts, wait and rerun manually only if additional charges are acceptable. The SDK has a default per-request timeout; this minimal app adds no retry or total-deadline logic. Keep raw errors off a shared screen because they can contain configuration.

Live requests consume quota and may incur Azure inference charges, including requests that time out locally. Consult the existing deployment's pricing. No resources or tickets are created; do not delete shared infrastructure.

```powershell
dotnet user-secrets clear --project .\SupportTriage.csproj
Remove-Item Env:Foundry__Endpoint, Env:Foundry__Model, Env:Foundry__ApiKey -ErrorAction SilentlyContinue
Remove-Item .\bin, .\obj -Recurse -Force -ErrorAction SilentlyContinue
```
