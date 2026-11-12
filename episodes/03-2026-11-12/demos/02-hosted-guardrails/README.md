# EBSearch MCP: local Responses host

Run the fixed-feed blog agent locally with `azd ai agent run`. The same
executable starts a read-only MCP server as a child process, discovers its two
tools, and exposes the agent through the Responses protocol.

## Prerequisites

- .NET SDK 11.0.100, PowerShell 7 and Azure CLI.
- An existing Foundry project and compatible model deployment.
- Azure Developer CLI with the Foundry agent extension. The local browser
  experience also needs the Agent Inspector extension.
- Direct packages: Microsoft.Agents.AI.Foundry.Hosting
  1.27.0-preview.261030.1 and ModelContextProtocol 2.4.0. The lockfile pins
  transitive packages.

The model call can incur Azure charges. The fixed public feeds also receive
the tool requests; do not use sensitive prompts.

## Configuration

From the episode directory run `.\setup.ps1`. The nonsecret
`AZURE_AI_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` bindings are
stored in unencrypted .NET user secrets. Environment variables override them.
Do not enter keys or tokens. `.\setup.ps1 -Validate` checks formats without
writing or calling Azure; `.\setup.ps1 -SelfTest` runs offline checks.

## Build and run

From this demo directory:

```powershell
dotnet restore .\ApplicationGuardrails.csproj --locked-mode
az login
azd ai agent run
```

The host uses `AzureCliCredential`. The command opens the local Agent Inspector;
use `azd ai agent run --no-client` for a headless run. Stop it with Ctrl+C.
The local host has no application authentication and may bind all interfaces.
Keep it on a trusted machine behind a firewall; do not expose or tunnel it.
Local execution does not exercise a Foundry hosted egress guardrail.

## Expected result

In the local browser, ask:

```text
Show me the latest blog post from Bruno Capuano from his personal blog and from the .NET Blog.
Use only the .NET Blog tool to summarize one recent post and cite its URL.
```

The terminal shows actual MCP tool calls and the response cites available
posts. Feed data and answers vary. For the hosted network-policy comparison,
use the separately deployed [EBSearch host](../03-hosted-guardrails/README.md).

## Cleanup

Stop the host with Ctrl+C. Remove local bindings with
`dotnet user-secrets clear --project .\ApplicationGuardrails.csproj` and unset
any environment variables you set. This local demo creates no cloud resources.

## Resources

- [Foundry hosted agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
- [Episode guide](../../README.md)
