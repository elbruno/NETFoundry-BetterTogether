# Wikipedia: local client for a hosted agent

This local Microsoft Agent Framework console calls a named Wikipedia hosted
agent using `AzureCliCredential`. The hosted agent owns its MCP tools and
managed identity. The endpoint routes to the active version configured in
Foundry; this client does not pin a version.

## Prerequisites and versions

Use .NET SDK 11.0.100, PowerShell 7, Azure CLI, and an existing Foundry project
with an authorized [Wikipedia hosted agent](../04-wikipedia-hosted/README.md).
Direct packages: Microsoft.Agents.AI.Foundry 1.28.0-preview.261106.1,
Azure.Identity 1.21.0 and Microsoft.Extensions.Configuration 11.0.0. The
lockfile pins transitive packages. Hosted sessions and model calls incur costs.

## Configure and run

From this directory:

```powershell
..\..\setup.ps1
az login
dotnet restore .\WikipediaClient.csproj --locked-mode
dotnet run --project .\WikipediaClient.csproj --no-restore
```

Setup saves nonsecret `FOUNDRY_PROJECT_ENDPOINT` and `FOUNDRY_AGENT_NAME`
(default `wikipedia-01`) to local user secrets. To target another deployed name,
set `$env:FOUNDRY_AGENT_NAME = 'wikipedia-02'`. Environment variables override
saved values. Edit the public question in `Program.cs`; expect a cited answer.
The community MCP service receives the question; do not send sensitive data.
Answers and policy decisions vary.

## Cleanup

Run `dotnet user-secrets clear --project .\WikipediaClient.csproj`, unset any
environment variables you set, and stop any hosted session created for the
demo. Do not delete shared Foundry resources.
