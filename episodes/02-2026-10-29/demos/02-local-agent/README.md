# Demo 02: Run the decision inside a local agent

This step puts the same Foundry decision call behind a custom Microsoft Agent
Framework `AIAgent`. A C# console app invokes the agent in-process. The agent
returns the selected team, confidence, full probability distribution,
provider-reported usage, and a deterministic suggestion. No HTTP server or
second chat model is involved.

The model, endpoint, ticket, question, and labels match
[Demo 01](../01-console-decision-model/README.md). The hosted version in Demo
03 uses the same agent behavior behind Foundry's Responses host.

## Tested versions

| Component | Version |
| --- | --- |
| .NET SDK | 10.0.403 |
| Microsoft Agent Framework | 1.22.0 |
| Azure.Identity | 1.17.0 |
| ElBruno.AI.Decisions | 0.6.3 |
| ElBruno.AI.Decisions.Foundry | 0.6.3 |

The lock file pins the resolved dependency graph. This sample was built for
Windows PowerShell.

## Prerequisites

- .NET SDK 10.0.403 or a later 10.0 patch.
- Azure CLI signed in with an identity authorized to call the existing decision
  model.
- The Foundry resource root URL and the name of its existing decision-model
  deployment.

The local app uses `AzureCliCredential`. Do not use an API key.
`FoundryDecisionClient` owns its HTTP transport when constructed with options
alone. Use a resource-root endpoint, not a project URL.

## Run

From this demo directory:

```powershell
dotnet restore .\tests\LocalRoutingAgent.Tests --locked-mode
dotnet test .\tests\LocalRoutingAgent.Tests -c Release --no-restore
dotnet build .\src\LocalRoutingAgent -c Release --no-restore

$env:Decision__Endpoint = 'https://<resource>.services.ai.azure.com/'
$env:Decision__Deployment = '<decision-deployment>'
dotnet run --project .\src\LocalRoutingAgent -c Release --no-launch-profile
```

The default ticket is `I was charged twice for my subscription.` To try another
synthetic ticket, edit the `ticket` constant in `Program.cs`. The commented
alternative uses `The sign-in page rejects my MFA code.` Run again to rebuild:

```powershell
dotnet run --project .\src\LocalRoutingAgent -c Release --no-launch-profile
```

The agent accepts one non-empty text ticket up to 64 KiB of UTF-8. Conversation
history, system instructions, and non-text input are unsupported. The
`manual-review` route is a model choice, not a guaranteed safety barrier.

## What changed from Demo 01

- The caller now invokes an `AIAgent`, not the decision client directly.
- The agent owns the input contract and the JSON response shape.
- The selected route and provider values are preserved; the suggestion text is
  a fixed application template, not generated reasoning.
- The same agent can be hosted remotely in Demo 03.

This sample only prints a suggestion. It does not update a ticket or prove model
accuracy. Local inference can incur Foundry charges. Use synthetic text only.

Remove the local configuration when finished:

```powershell
Remove-Item Env:Decision__Endpoint, Env:Decision__Deployment -ErrorAction SilentlyContinue
```

The custom agent pattern is adapted from the Microsoft Foundry sample cited in
the [Demo 03 source attribution](../01-hosted-decision-agent/THIRD-PARTY-NOTICES.md).

Continue to [Demo 03: Run the agent as a Foundry Hosted Agent](../03-foundry-hosted-agent/README.md).
