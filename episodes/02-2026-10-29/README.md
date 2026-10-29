# Episode 2: .NET + Foundry, Better Together: Decision Models in Action

**Date:** October 29, 2026, 12:00-1:00 PM EDT (16:00-17:00 UTC).

**Event and registration:** [Microsoft Reactor](https://developer.microsoft.com/en-us/reactor/events/27631/)

## Hosts

- [Bruno Capuano](https://aka.ms/elbruno)
- [Jose Luis Latorre](https://www.linkedin.com/in/joslat/), co-speaker

## Overview

This episode focuses on two topics: Hosted Agents using decision models and
agent memory. The support-routing demo progresses through four stages: a C#
console app calls a decision model in Microsoft Foundry, a local Microsoft
Agent Framework agent calls the same model, Aspire displays the local agent's
GenAI traces, and a Foundry Hosted Agent runs that agent. Co-speaker Jose Luis
Latorre joins to compare Foundry Memory Stores
with custom AgentMemory.

Using custom AgentMemory from a Foundry Hosted Agent is exploratory, is not
part of the current support-routing prototype, and has not been verified.
AgentEval and hybrid evaluation are possible related discussion if time allows.
GraphBridge and GraphRAG may be mentioned only if ready and Foundry-aligned.
Jose's repositories and resources will be added when shared and reviewed.

The example uses a synthetic support ticket and returns a bounded team
suggestion. It does not update a ticketing system. The session explains both
identity hops: the caller to the hosted endpoint, and the hosted runtime's
agent-instance identity to the decision-model resource. Authentication
identifies the caller; Azure RBAC authorizes the identity to access Azure
resources. This is distinct from assigning Microsoft Entra directory roles or
Microsoft Graph permissions.

## Recording

The recording link will be added after the episode.

## Demos and resources

### Configure all four local demos once

From the repository root, run in the PowerShell terminal you will use for the demos:

```powershell
.\episodes\02-2026-10-29\Setup-UserSecrets.ps1
```

The script asks once for the Foundry resource-root HTTPS URL and once for the
existing decision deployment name. It sets `Decision__Endpoint` and
`Decision__Deployment` in the current process for demos 01-03 and Aspire,
and saves `Decision:Endpoint` and `Decision:Deployment` to Demo 04's existing
.NET user-secrets store. It also saves the environment values for your Windows
user, replacing any previous values with those names. Other apps using these
names will also see them. Restart existing VS Code windows and terminals to
pick up the persisted environment. Use `-SessionOnly` to skip user-level
environment persistence; the hosted user-secrets settings are still saved.

Run the script directly as shown, not via a separate `pwsh -File` process,
if you want the current terminal to receive the environment values. Run before
recording, since prompt input is visible. User Secrets are unencrypted local
development storage, not a production secret store. No API key is needed.
Sign in separately with `az login` using an identity authorized for inference.
Setup makes no Azure calls, changes no permissions, and does not configure or
deploy the remote hosted agent. The existing per-demo environment setup can
be skipped after running this script.

To remove the persisted environment settings:

```powershell
[Environment]::SetEnvironmentVariable('Decision__Endpoint', $null, 'User')
[Environment]::SetEnvironmentVariable('Decision__Deployment', $null, 'User')
Remove-Item Env:Decision__Endpoint, Env:Decision__Deployment -ErrorAction SilentlyContinue
dotnet user-secrets remove 'Decision:Endpoint' --project .\episodes\02-2026-10-29\demos\04-foundry-hosted-agent\src\SupportRoutingAgent
dotnet user-secrets remove 'Decision:Deployment' --project .\episodes\02-2026-10-29\demos\04-foundry-hosted-agent\src\SupportRoutingAgent
```

Check setup without changing configuration:

```powershell
.\episodes\02-2026-10-29\Setup-UserSecrets.ps1 -ValidateOnly
pwsh -File .\episodes\02-2026-10-29\tests\Setup-UserSecrets.Tests.ps1
```

1. [Call a Foundry decision model from C#](demos/01-console-decision-model/README.md):
   a console app makes a direct, structured decision-model call.
2. [Run the decision inside a local agent](demos/02-local-agent/README.md):
   a console app invokes a Microsoft Agent Framework agent in-process.
3. [Inspect local agent GenAI traces with Aspire](demos/03-local-agent-aspire/README.md):
   a real local agent project and AppHost export agent and native-model spans.
4. [Run the agent as a Foundry Hosted Agent](demos/04-foundry-hosted-agent/README.md):
   the existing hosted implementation, retested successfully on October 28.
   Repeatable deployment instructions, a remote C# caller, and live evaluation
   fixtures remain incomplete; the current Foundry User Azure RBAC assignment
   is broad and is not a least-privilege recommendation.

Every numbered directory contains runnable source. The earlier duplicate
`01-hosted-decision-agent` directory and README-only `03-foundry-hosted-agent`
guide were replaced by this corrected four-stage layout.

## Additional resources

- [Everything announced at GitHub Universe 2026: opening keynote recap](https://www.youtube.com/watch?v=xbCa0aa4tRM)
- [GitHub Universe 2026](https://githubuniverse.com/)
- [New Year, New Bot](https://aka.ms/newyearnewbot)

## Guest demos and resources

Jose's repositories and related resources are pending and will be added after
they are shared and reviewed.

[Back to the series](../../README.md)
