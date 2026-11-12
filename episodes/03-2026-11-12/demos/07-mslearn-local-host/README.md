# Microsoft Learn MCP: local Responses host

Run an agent using the official Microsoft Learn MCP tools as a local Responses
host. The model uses `AzureCliCredential`; no Azure token is sent to Learn.

## Prerequisites and versions

Use .NET SDK 11.0.100, PowerShell 7, Azure CLI, Azure Developer CLI with the
Foundry agent and Agent Inspector extensions, and an existing Foundry project
and compatible model deployment. Direct packages: Microsoft.Agents.AI.Foundry.Hosting
1.28.0-preview.261106.1, ModelContextProtocol 2.4.0 and Azure.Identity 1.21.0;
the lockfile pins transitive packages. Model calls can incur Azure charges.

## Configure and run

From this directory:

```powershell
..\..\setup.ps1
az login
dotnet restore .\LearnLocal.csproj --locked-mode
azd ai agent run
```

Setup stores nonsecret `AZURE_AI_PROJECT_ENDPOINT` and
`AZURE_AI_MODEL_DEPLOYMENT_NAME` in local user secrets. Environment variables
override them. The command opens the local Agent Inspector; use
`azd ai agent run --no-client` for a headless run.

Ask: `Find official Microsoft Learn documentation explaining async programming in C#.`
Expect an official link and MCP tool-call messages in the terminal. Responses
vary. Use public questions only. This unauthenticated local host may bind all
interfaces: keep it behind a local firewall and do not expose or tunnel it.
Local runs do not test hosted policies.

## Cleanup

Stop the host with Ctrl+C, run
`dotnet user-secrets clear --project .\LearnLocal.csproj`, and unset any
environment variables you set. The sample creates no cloud resources. See the
[hosted version](../08-mslearn-hosted/README.md).
