# EBSearch: local client for a hosted agent

This local Microsoft Agent Framework console calls a named EBSearch hosted
agent using `AzureCliCredential`. The hosted agent owns its MCP tools and
managed identity. The endpoint routes to the active version configured in
Foundry; this client does not pin a version.

## Prerequisites and versions

Use .NET SDK 11.0.100, PowerShell 7, Azure CLI, and an existing Foundry project
with an authorized [EBSearch hosted agent](../03-hosted-guardrails/README.md).
Direct packages: Microsoft.Agents.AI.Foundry 1.28.0-preview.261106.1,
Azure.Identity 1.21.0 and Microsoft.Extensions.Configuration 11.0.0. The
lockfile pins transitive packages. Hosted sessions and model calls incur costs.

## Configure and run

From this directory:

```powershell
..\..\setup.ps1
az login
dotnet restore .\EBSearchClient.csproj --locked-mode
dotnet run --project .\EBSearchClient.csproj --no-restore
```

Setup saves nonsecret `FOUNDRY_PROJECT_ENDPOINT` and `FOUNDRY_AGENT_NAME`
(default `ebsearch-01`) to local user secrets. To target another deployed name,
set `$env:FOUNDRY_AGENT_NAME = 'ebsearch-02'`. Environment variables override
saved values. Edit the public question in `Program.cs`; expect an answer with a
blog link. Feed content, policy decisions and answers vary.

## Cleanup

Run `dotnet user-secrets clear --project .\EBSearchClient.csproj`, unset any
environment variables you set, and stop any hosted session created for the
demo. Do not delete shared Foundry resources.
