# Microsoft Learn: local client for a hosted agent

This local Microsoft Agent Framework console calls a named Microsoft Learn
hosted agent using `AzureCliCredential`. The hosted agent owns its MCP tools and
managed identity. The endpoint routes to the active version configured in
Foundry; this client does not pin a version.

## Prerequisites and versions

Use .NET SDK 11.0.100, PowerShell 7, Azure CLI, and an existing Foundry project
with an authorized [Learn hosted agent](../08-mslearn-hosted/README.md).
Direct packages: Microsoft.Agents.AI.Foundry 1.28.0-preview.261106.1,
Azure.Identity 1.21.0 and Microsoft.Extensions.Configuration 11.0.0. The
lockfile pins transitive packages. Hosted sessions and model calls incur costs.

## Configure and run

From this directory:

```powershell
..\..\setup.ps1
az login
dotnet restore .\LearnClient.csproj --locked-mode
dotnet run --project .\LearnClient.csproj --no-restore
```

Setup saves nonsecret `FOUNDRY_PROJECT_ENDPOINT` and `FOUNDRY_AGENT_NAME`
(default `mslearn-01`) to local user secrets. To target another deployed name,
set `$env:FOUNDRY_AGENT_NAME = 'mslearn-02'`. Environment variables override
saved values. Edit the public question in `Program.cs`; expect a cited Learn
documentation link. Answers and policy decisions vary.

The console runs one question and is not interactive. Choose one line for
`Program.cs` and rerun:

```csharp
const string question = "Use Microsoft Learn to find official Microsoft Foundry guardrail intervention-point documentation and cite the link.";
```

```csharp
const string question = "Use Microsoft Learn to find hosted-agent network egress documentation and cite the link.";
```

## Cleanup

Run `dotnet user-secrets clear --project .\LearnClient.csproj`, unset any
environment variables you set, and stop any hosted session created for the
demo. Do not delete shared Foundry resources.
