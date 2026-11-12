# EBSearch MCP: Foundry hosted agents

Deploy one read-only EBSearch image as `ebsearch-01`, `ebsearch-02`, and
`ebsearch-03`. Each agent starts the same stdio MCP server and uses its two
fixed-feed tools. The container uses `DefaultAzureCredential` and the Foundry
agent-instance managed identity for the model. Compare agent-level guardrails
manually in the Foundry portal; the names alone do not configure a policy.

## Prerequisites and versions

- .NET SDK 11.0.100, PowerShell 7, Azure CLI, and Azure Developer CLI with the
  Foundry agent extension.
- An existing Foundry project, compatible model deployment, and Azure Container
  Registry connected to that project. The hosted identity must be authorized
  to use the model and pull the image.
- Direct packages: Microsoft.Agents.AI.Foundry.Hosting
  1.27.0-preview.261030.1 and ModelContextProtocol 2.4.0. The lockfile pins
  transitive packages. The image uses .NET SDK 11.0.100 and ASP.NET 11.0.0.

Registry builds and storage, hosted sessions, and model calls can incur Azure
charges. Only public prompts and fixed public feeds should be used.

## Build and deploy

Run these commands from this directory. Replace the placeholders with your own
existing resources; do not put credentials in the commands. The project ARM
ID ends in `/projects/<project-name>`. The registry must already have a
Container Registry connection in the Foundry project.

```powershell
dotnet restore .\ApplicationGuardrailsHosted.csproj --locked-mode
dotnet build .\ApplicationGuardrailsHosted.csproj --no-restore
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

azd env new episode03-ebsearch
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

az acr build --registry $registry --image 'episode03/ebsearch:demo' --file .\Dockerfile .
if ($LASTEXITCODE -ne 0) { throw 'Image build failed.' }
$digest = az acr repository show --name $registry --image 'episode03/ebsearch:demo' --query digest --output tsv
if ($LASTEXITCODE -ne 0 -or -not $digest) { throw 'Could not resolve the image digest.' }
azd env set EBSEARCH_AGENT_IMAGE "$($registryInfo.loginServer)/episode03/ebsearch@$digest"
foreach ($name in 'ebsearch-01', 'ebsearch-02', 'ebsearch-03') {
    azd deploy $name --no-prompt
    if ($LASTEXITCODE -ne 0) { throw "Deployment failed: $name" }
}
```

The allowlisted `.dockerignore` sends only the Dockerfile and required source
files to the registry build. Each deployment uses the same immutable image
digest. Deploying a name that already exists creates a new agent version;
use only names you are authorized to manage.

## Try it in Foundry

Open each named agent in the Foundry portal, start a fresh session, and use the
same prompts for comparisons:

```text
Show me the latest blog post from Bruno Capuano from his personal blog and from the .NET Blog.
Use only the .NET Blog tool to summarize one recent post and cite its URL.
```

Expect tool calls and links for available feeds. A feed can fail independently
of a guardrail. Configure any agent-level policy separately, then use fresh
sessions and inspect policy decision evidence before attributing a denial to
that policy. The MCP child process makes the feed requests; a parent tool span
alone does not prove a network-policy decision. Model-deployment content
filters affect every consumer of that deployment.

To inspect a deployment or invoke it outside the portal:

```powershell
azd ai agent show ebsearch-01 --output json
azd ai agent invoke ebsearch-01 'Use the .NET Blog tool and cite a recent post.' --new-session --new-conversation --protocol responses
```

The [local client](../11-ebsearch-hosted-client/README.md) calls the hosted
agent from a MAF console. Its endpoint selects the active Foundry version.

## Cleanup

Record the session ID returned by your invocation. List and stop only sessions
you created:

```powershell
azd ai agent sessions list --agent-name ebsearch-01 --output table
azd ai agent sessions stop '<your-session-id>' --agent-name ebsearch-01
dotnet user-secrets clear --project .\ApplicationGuardrailsHosted.csproj
```

Stopping preserves the session filesystem. Remove only agent versions, images,
or other resources you own and no longer need; do not delete shared resources.

## Resources

- [Foundry hosted agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
- [Local Responses host](../02-hosted-guardrails/README.md)
- [Episode guide](../../README.md)
