# Hello Decisions

One console app, one support message, one decision. Open **Program.cs** and read
it from top to bottom:

1. Create the Foundry decision client using external configuration.
2. Define the message and three allowed teams, then call `ChooseAsync`.
3. Print the selected team, option probabilities, and provider confidence.

There are no command-line options, helper classes, or alternative transports.
Change the message directly in `Program.cs` to explore another scenario.
The model returns a bounded decision, not a generated answer or explanation.

## Prerequisites

- .NET SDK **10.0.401**, with patch roll-forward defined in `global.json`.
- An existing **Microsoft-Decision-1** deployment in Microsoft Foundry.
- Azure CLI signed in with `az login` and permission to invoke the deployment,
  or an API key configured privately.
- **ElBruno.AI.Decisions.Foundry 0.6.2**, a community SDK, not an official
  Microsoft SDK. Configuration packages are pinned to **10.0.0**.

## Configure off stream

From this demo directory:

```powershell
pwsh -File ..\..\Setup-UserSecrets.ps1
```

The episode setup script configures `Foundry:Endpoint` (resource root URL),
`Foundry:Model` (deployment name, not necessarily the catalog model name), and
optional `Foundry:ApiKey`. This sample's user-secrets ID remains
`netfoundry-episode01-hello-decisions`. Without a key, the client uses Azure CLI
identity. Environment variables with `__` in place of `:` override user secrets.
Never put real values in source or show secrets during a recording.

## Run

From this directory:

```powershell
dotnet restore --locked-mode
dotnet run
```

Expected output shape:

```text
Message: I was charged twice for my subscription.
Selected team: <selected label>
  billing: <probability>
  technical_support: <probability>
  sales: <probability>
Provider confidence: <confidence>
```

Results vary. Selected-label probability and provider confidence are distinct
signals, neither a guarantee of correctness. This sample does not move a ticket.

## Errors, costs, and cleanup

This intentionally minimal teaching example lets configuration and SDK exceptions
surface normally. If a request fails, stop screen sharing before investigating
diagnostics, then check configuration, `az login`, and deployment access.
There is no fallback or custom error handling.

Each run makes one chargeable Foundry inference request. The sample creates no
resources; do not delete an existing shared deployment. To remove this sample's
local settings, run off stream:

```powershell
dotnet user-secrets clear --project .\HelloDecisions.csproj
```

Also remove any `Foundry__*` environment variables you configured.

## Resources

- [Native decision API documentation](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision)
- [Community SDK](https://github.com/elbruno/ElBruno.AI.Decisions)
- [Episode](../../README.md)
