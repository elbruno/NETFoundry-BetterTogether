# Wikipedia MCP: local console

A local Microsoft Agent Framework agent discovers Wikipedia search and summary
tools and asks two editable public questions. It uses `AzureCliCredential` for
the Foundry model. The MCP server is a
[community service](https://github.com/cyanheads/wikipedia-mcp-server), not
operated by Wikimedia. Queries are sent to `https://wikipedia.caseyjhand.com/mcp`;
never send sensitive data.

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
dotnet restore .\WikipediaConsole.csproj --locked-mode
dotnet run --project .\WikipediaConsole.csproj --no-restore
```

Setup stores nonsecret `FOUNDRY_PROJECT_ENDPOINT`,
`AZURE_OPENAI_DEPLOYMENT_NAME`, and `WIKIPEDIA_MCP_ENDPOINT` in local user
secrets. Environment variables override them. The console logs actual MCP tool
calls, then prints answers with article links. Responses and service availability
vary. Edit the questions in `Program.cs`.

## Cleanup

Run `dotnet user-secrets clear --project .\WikipediaConsole.csproj` and unset
any environment variables you set. The sample creates no cloud resources. See
the [local host](../06-wikipedia-local-host/README.md) and
[hosted version](../04-wikipedia-hosted/README.md).
