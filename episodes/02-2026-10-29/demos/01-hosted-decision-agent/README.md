# Demo 03 implementation: Foundry Hosted Agent feasibility prototype

This implementation remains at its original published path so existing links
continue to work. The episode's numbered progression starts at
[Demo 01](../01-console-decision-model/README.md) and
[Demo 02](../02-local-agent/README.md); see the
[Demo 03 guide](../03-foundry-hosted-agent/README.md) for this stage's summary.

**Status: feasibility confirmed, demo incomplete.** Local inference and the
Responses adapter work, and a live synthetic request succeeded through the
deployed Foundry Hosted Agent using its agent-instance identity to call the
native decision endpoint without an API key. Reproducible deployment
instructions, a remote C# caller, and live evaluation fixtures remain
incomplete, so do not present this prototype as a finished audience demo.

This sample uses a custom Microsoft Agent Framework `AIAgent` to ask
Microsoft-Decision-1 which team should handle a synthetic support ticket.
`ElBruno.AI.Decisions.Foundry` calls the native SystemOne API. It does not wrap the
decision model as a chat-completions model or use a second chat model.

```text
user ticket -> Responses host -> custom AIAgent -> decision SDK -> SystemOne
           <- Responses output containing typed JSON and a templated suggestion
```

## Tested versions

| Component | Version |
| --- | --- |
| .NET SDK | 10.0.403 |
| Microsoft.Agents.AI | 1.22.0 |
| Microsoft.Agents.AI.Foundry.Hosting | 1.22.0-preview.260918.1 |
| Azure.AI.AgentServer.Core | 1.0.0-beta.29 |
| Azure.AI.AgentServer.Responses | 1.0.0-beta.8 |
| Azure.Identity | 1.21.0 |
| ElBruno.AI.Decisions.Foundry | 0.6.3 |
| Azure Developer CLI | 1.36.0 |
| azure.ai.agents extension | 1.0.0-beta.20 |

Package lock files preserve transitive dependencies. This prototype was checked
on Windows with PowerShell. Hosting packages and CLI extensions are previews.

## Run locally

Prerequisites: the .NET SDK above, Azure CLI signed in as an identity permitted to
invoke an existing Microsoft-Decision-1 deployment, and that deployment's resource
root and deployment name. No API key is used.

From this demo directory:

```powershell
dotnet restore .\src\SupportRoutingAgent --locked-mode
dotnet test .\tests\SupportRoutingAgent.Tests -c Release

$env:Decision__Endpoint = 'https://<resource>.services.ai.azure.com/'
$env:Decision__Deployment = '<decision-deployment>'
$env:PORT = '8097'
dotnet run --project .\src\SupportRoutingAgent -c Release --no-launch-profile
```

In another PowerShell terminal:

```powershell
Invoke-RestMethod 'http://localhost:8097/readiness'
$body = @{
    input = 'I was charged twice for my subscription.'
    stream = $false
    store = $false
} | ConvertTo-Json
$response = Invoke-RestMethod 'http://localhost:8097/responses' `
    -Method Post -ContentType 'application/json' -Body $body
if ($response.status -ne 'completed') { throw "Agent response: $($response.status)" }
$response.output[0].content[0].text | ConvertFrom-Json
```

Expected shape, not guaranteed inference values:

```json
{
  "team": "billing",
  "confidence": 0.61,
  "probabilities": {
    "billing": 0.7,
    "technical-support": 0.1,
    "account-access": 0.1,
    "manual-review": 0.1
  },
  "usage": {
    "inputTokens": 12,
    "outputTokens": 0,
    "totalTokens": null,
    "reasoningTokens": null,
    "cachedTokens": null
  },
  "answer": "Suggested team: billing. A person should confirm the route before taking action."
}
```

These numeric values are synthetic fixtures used by the tests. Provider confidence
is not the chosen label's probability or a guarantee of correctness. Missing usage
stays null; reported zero stays zero. No ticket is created or assigned.

The agent accepts one non-empty text user message, at most 64 KiB in UTF-8.
Conversation history, system instructions, and non-text content are unsupported.
Streaming emits a complete decision, not generated reasoning. A provider failure
remains a failed response, not a `manual-review` decision.

**Local hosting has no application authentication.** It is for trusted local
development only. The hosting SDK can bind all interfaces. Do not expose or tunnel
this port, and keep it behind the local firewall.

## Offline checks

The tests use explicitly synthetic decisions, fake HTTP, and a fake credential.
They make no Azure calls and are not evidence of model accuracy or hosted identity
support. They cover all routes, serialization, usage semantics, request isolation,
invalid input, cancellation, native request URL/audience/authentication, provider
errors, and the real Responses HTTP/SSE adapter.

```powershell
dotnet restore .\tests\SupportRoutingAgent.Tests --locked-mode
dotnet test .\tests\SupportRoutingAgent.Tests -c Release --no-restore
```

## Hosted feasibility result and remaining work

The checked configuration successfully code-deployed this host with .NET 10 and
Responses protocol 2.0.0. An authenticated caller reached it. Earlier outbound
native model requests returned HTTP 401 with provider code `PermissionDenied`,
including during an earlier resource-scoped Foundry User assignment experiment.
After the current account-scoped Foundry User Azure RBAC assignment was verified,
a fresh hosted request with a synthetic duplicate-charge ticket completed and
returned `billing`. The hosted call used the agent-instance identity and no API
key. This confirms feasibility in the current deployment, but does not establish
that this broad role is least privilege or that the setup is reproducible across
environments. Anonymous inbound invocation returned 401.

The earlier failures identify the outbound boundary but do not explain why the
current assignment works. Do not widen permissions, embed an API key, or
substitute a developer credential in the hosted environment as an automatic
workaround.

`azure.yaml` references external environment values instead of a real project.
It deliberately contains no new model deployment. Full deployment/RBAC
instructions, a remote C# caller, live evaluation fixtures, and a completed
end-to-end walkthrough remain incomplete. The successful request confirms
feasibility in the current deployment, not a reproducible setup or a
least-privilege role recommendation.

Permission boundaries to resolve before expanding the sample:

| Principal | Required access |
| --- | --- |
| Developer running locally | Native decision inference on the existing resource |
| Hosted-agent deployer | Foundry project deployment permissions |
| Remote caller | Permission to invoke the agent endpoint |
| Agent instance | Azure RBAC access to the decision resource; Foundry User at account scope succeeded in this environment, but is broad and not established as least privilege |

Do not confuse the project endpoint with the decision resource root or the
project managed identity with the runtime's agent-instance identity.

## Costs, cleanup, and safety

Local inference still incurs Azure model charges. Hosted sessions use billable
compute; deployment artifacts and connected telemetry can incur storage,
build, or ingestion charges. Prices depend on region and configuration:
[Foundry pricing](https://azure.microsoft.com/pricing/details/ai-foundry/).
This prototype is not a cost or performance benchmark.

Stop the local host with Ctrl+C before rebuilding or deploying to avoid Windows
executable locks. Remove session-specific environment values when finished:

```powershell
Remove-Item Env:Decision__Endpoint, Env:Decision__Deployment, Env:PORT -ErrorAction SilentlyContinue
```

Hosted test compute was stopped after the feasibility experiment. Agent
definitions, versions, retained session volumes, and platform artifacts may
remain. If you deploy your own copy, inventory only the resources and role
assignments it creates. Review the current CLI's session-stop and agent-delete
commands before cleanup. Never run blanket resource-group deletion or `azd down`
against a reused project.

Do not commit `.azure`, real configuration, tokens, or logs. Use synthetic tickets
only. Message-content telemetry is disabled in the manifest. The transport logger
records status codes, not request bodies, credentials, or provider response bodies.
Normal Foundry protocol storage is separate from telemetry and may persist
conversation content; do not use real customer tickets.

## Reading order and references

Read `RoutingDecision.cs`, `SupportRoutingAgent.cs`, then `Program.cs`.
`DecisionTransport.cs` logs only the HTTP status of the native call.

- [Agent Framework hosting](https://learn.microsoft.com/azure/foundry/how-to/develop/framework-hosted-agents?pivots=programming-language-csharp)
- [Hosted token service and agent-instance identity](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agent-token-service)
- [Hosted-agent permissions](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agent-permissions)
- [Decision SDK](https://github.com/elbruno/ElBruno.AI.Decisions)
- [Source attribution](THIRD-PARTY-NOTICES.md)
