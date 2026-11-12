# Microsoft Learn MCP: local console

A local Microsoft Agent Framework agent uses the official Microsoft Learn MCP
server at `https://learn.microsoft.com/api/mcp` to search documentation and
code samples. It asks two editable public questions and authenticates to the
Foundry model with `AzureCliCredential`. No Azure token is sent to Learn.

## Prerequisites and versions

Use .NET SDK 11.0.100, PowerShell 7, Azure CLI, and an existing Foundry project
and compatible model deployment. Direct packages: Microsoft.Agents.AI.Foundry
1.28.0-preview.261106.1, ModelContextProtocol.Core 2.4.0, Azure.Identity
1.21.0, and Microsoft.Extensions.Configuration 11.0.0. The lockfile pins
transitive dependencies. Model calls can incur Azure charges.

## Configure and run

From this directory:

```powershell
..\..\setup.ps1
az login
dotnet restore .\LearnConsole.csproj --locked-mode
dotnet run --project .\LearnConsole.csproj --no-restore
```

Setup stores nonsecret `FOUNDRY_PROJECT_ENDPOINT` and
`AZURE_OPENAI_DEPLOYMENT_NAME` in local user secrets. Environment variables
override them. The console logs actual MCP calls and returns documentation or
sample links; results vary. Use only public questions and edit the examples in
`Program.cs`.

## Cleanup

Run `dotnet user-secrets clear --project .\LearnConsole.csproj` and unset any
environment variables you set. This sample creates no cloud resources. See the
[local host](../07-mslearn-local-host/README.md) and
[hosted version](../08-mslearn-hosted/README.md).
