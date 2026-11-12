# Microsoft Learn MCP: Foundry hosted agents

Deploy one Microsoft Learn MCP image as `mslearn-01`, `mslearn-02`, and
`mslearn-03`. Each agent discovers the official documentation and code-sample
tools. The container uses `DefaultAzureCredential` with the Foundry
agent-instance managed identity for the model; no Azure token is sent to the
public Learn MCP endpoint. Configure agent-level guardrails manually in the
Foundry portal; different names do not themselves apply a policy.

## Prerequisites and versions

- .NET SDK 11.0.100, PowerShell 7, Azure CLI, and Azure Developer CLI with the
  Foundry agent extension.
- An existing Foundry project, compatible model deployment, and Azure Container
  Registry connected to that project. The hosted identity must be authorized
  to use the model and pull the image.
- Direct packages: Microsoft.Agents.AI.Foundry.Hosting
  1.28.0-preview.261106.1, ModelContextProtocol 2.4.0 and Azure.Identity
  1.21.0. The lockfile pins transitive packages. The image uses .NET SDK
  11.0.100 and ASP.NET 11.0.0.

Registry builds and storage, hosted sessions, and model calls can incur Azure
charges. Send only public questions to `https://learn.microsoft.com/api/mcp`.

## Build and deploy

Run these commands from this directory. Replace the placeholders with your own
existing resources; do not put credentials in the commands. The project ARM
ID ends in `/projects/<project-name>`. The registry must already have a
Container Registry connection in the Foundry project.

```powershell
dotnet restore .\LearnHosted.csproj --locked-mode
dotnet build .\LearnHosted.csproj --no-restore
az login
azd auth login

$projectEndpoint = '<your-foundry-project-endpoint>'
$projectId = '<your-foundry-project-arm-id>'
$model = '<your-model-deployment>'
$registry = '<your-existing-registry-name>'
$resourceGroup = '<your-foundry-resource-group>'
$location = '<your-foundry-region>'
$account = '<your-foundry-account-name>'
$project = '<your-foundry-project-name>'
$registryInfo = az acr show --name $registry --output json | ConvertFrom-Json

azd env new episode03-mslearn
azd env set AZURE_AI_PROJECT_ENDPOINT $projectEndpoint
azd env set FOUNDRY_PROJECT_ENDPOINT $projectEndpoint
azd env set AZURE_AI_PROJECT_ID $projectId
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME $model
azd env set AZURE_AI_ACCOUNT_NAME $account
azd env set AZURE_AI_PROJECT_NAME $project
azd env set AZURE_RESOURCE_GROUP $resourceGroup
azd env set AZURE_LOCATION $location
azd env set AZURE_SUBSCRIPTION_ID (az account show --query id --output tsv)
azd env set AZURE_TENANT_ID (az account show --query tenantId --output tsv)
azd env set AZURE_CONTAINER_REGISTRY_NAME $registry
azd env set AZURE_CONTAINER_REGISTRY_ENDPOINT $registryInfo.loginServer
azd env set AZURE_CONTAINER_REGISTRY_RESOURCE_ID $registryInfo.id
azd env set USE_EXISTING_AI_PROJECT true
azd env set AZD_AGENT_SKIP_ACR true
azd env set AZD_FOUNDRY_ACR_MODE none

az acr build --registry $registry --image 'episode03/mslearn:demo' --file .\Dockerfile .
if ($LASTEXITCODE -ne 0) { throw 'Image build failed.' }
$digest = az acr repository show --name $registry --image 'episode03/mslearn:demo' --query digest --output tsv
if ($LASTEXITCODE -ne 0 -or -not $digest) { throw 'Could not resolve the image digest.' }
azd env set MSLEARN_AGENT_IMAGE "$($registryInfo.loginServer)/episode03/mslearn@$digest"
foreach ($name in 'mslearn-01', 'mslearn-02', 'mslearn-03') {
    azd deploy $name --no-prompt
    if ($LASTEXITCODE -ne 0) { throw "Deployment failed: $name" }
}
```

The allowlisted `.dockerignore` sends only the Dockerfile and required source
files to the registry build. Each deployment uses the same immutable image
digest. Deploying a name that already exists creates a new agent version;
use only names you are authorized to manage.

## Try it in Foundry

Open each named agent in the Foundry portal and ask:

```text
Find official Microsoft Learn documentation explaining async programming in C#.
```

Expect an official documentation link and MCP tool calls. Availability and
answers vary. Configure any agent-level policy separately, use fresh sessions,
and inspect actual policy decision evidence before claiming enforcement.
Model-deployment content filters affect every consumer of that deployment.

To inspect a deployment or invoke it outside the portal:

```powershell
azd ai agent show mslearn-01 --output json
azd ai agent invoke mslearn-01 'Find official C# async programming documentation.' --new-session --new-conversation --protocol responses
```

The [local client](../13-mslearn-hosted-client/README.md) calls the hosted
agent from a MAF console. Its endpoint selects the active Foundry version.

## Cleanup

Record the session ID returned by your invocation. List and stop only sessions
you created:

```powershell
azd ai agent sessions list --agent-name mslearn-01 --output table
azd ai agent sessions stop '<your-session-id>' --agent-name mslearn-01
dotnet user-secrets clear --project .\LearnHosted.csproj
```

Stopping preserves the session filesystem. Remove only agent versions, images,
or other resources you own and no longer need; do not delete shared resources.

## Resources

- [Microsoft Learn MCP](https://learn.microsoft.com/training/support/mcp)
- [Foundry hosted agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
- [Local Responses host](../07-mslearn-local-host/README.md)
- [Episode guide](../../README.md)
