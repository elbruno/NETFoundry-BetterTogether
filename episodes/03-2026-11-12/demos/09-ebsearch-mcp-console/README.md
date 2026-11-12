# EBSearch MCP: local console

An agent discovers two read-only MCP tools backed by fixed public RSS feeds and
asks two editable questions. The MCP server runs as a child process over stdio;
the model uses `AzureCliCredential`. No caller can supply a feed URL.

## Prerequisites and versions

Use .NET SDK 11.0.100, PowerShell 7, Azure CLI, and an existing Foundry project
and compatible model deployment. Direct packages: Microsoft.Agents.AI.Foundry
1.28.0-preview.261106.1, ModelContextProtocol 2.4.0, Azure.Identity 1.21.0,
and Microsoft.Extensions.Hosting and Configuration 11.0.0. The lockfile pins
transitive dependencies. The model can incur Azure charges.

## Configure and run

From this directory:

```powershell
..\..\setup.ps1
az login
dotnet restore .\EBSearchConsole.csproj --locked-mode
dotnet run --project .\EBSearchConsole.csproj --no-restore
```

Setup stores nonsecret `FOUNDRY_PROJECT_ENDPOINT` and
`AZURE_OPENAI_DEPLOYMENT_NAME` in local user secrets. Alternatively set those
environment variables; they take precedence. Edit the questions in
`Program.cs`. The console logs actual MCP calls and prints answers with source
links. Feed content and answers vary; use only public questions.

The console is not interactive. Its `questions` array runs these examples
automatically; edit the array and rerun to change them:

```text
Use the .NET Blog tool to summarize one recent post and cite its URL.
Use the personal blog tool to summarize one recent post and cite its URL.
```

## Cleanup

Run `dotnet user-secrets clear --project .\EBSearchConsole.csproj` and unset any
environment variables you set. The child server stops with the console; this
sample creates no cloud resources. See the [local host](../02-hosted-guardrails/README.md)
and [hosted version](../03-hosted-guardrails/README.md).
