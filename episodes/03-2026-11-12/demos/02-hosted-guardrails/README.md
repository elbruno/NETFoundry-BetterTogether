# Hosted agent network guardrails

This C# Responses agent exposes two read-only functions for fixed RSS feeds.
Compare its behavior in a Foundry-hosted runtime with and without a network
egress guardrail. These functions are not the Toolbox tools from demo 1.

## Prerequisites

- .NET SDK 11.0.100, PowerShell 7 and Azure CLI.
- An existing Foundry project, compatible model deployment, hosted-agent
  permissions and an accessible container registry for the Linux/amd64 image.
- For deployment, Azure Developer CLI with the Foundry agent extension and a
  configured environment. Network egress guardrails are a Foundry preview.
- Direct package: Microsoft.Agents.AI.Foundry.Hosting
  1.27.0-preview.261030.1. The lockfile pins transitive packages.

The SDK and package versions were used in the original sample; rehearse this
episode's adaptation and deployed versions in your own environment. Model,
hosting, container registry and telemetry usage may incur cloud charges.

## Configuration

From the episode directory run `.\setup.ps1`. Enter the nonsecret Toolbox URL
for demo 1, plus the Foundry project endpoint and model deployment for this
demo. Values are stored in unencrypted .NET user secrets for local development.
Do not enter keys or tokens. Alternatively set `AZURE_AI_PROJECT_ENDPOINT` and
`AZURE_AI_MODEL_DEPLOYMENT_NAME` as environment variables. Environment variables
override user secrets, including empty values.

Run `.\setup.ps1 -Validate` for a non-mutating format check and
`.\setup.ps1 -SelfTest` for offline checks. Neither validates Azure access,
the runtime identity, the registry or network policy.

## Build and run

From this demo directory:

```powershell
dotnet restore .\ApplicationGuardrails.csproj --locked-mode
dotnet build .\ApplicationGuardrails.csproj --no-restore
dotnet run --project .\ApplicationGuardrails.csproj --no-build
```

The last command starts a local Responses host; stop it with Ctrl+C. Local
execution does **not** exercise the Foundry hosted egress guardrail.

For the network comparison, configure an existing Azure Developer CLI
environment with the project and model deployment values used in `azure.yaml`,
then review the target and deploy this directory's `better-together-guardrails`
service using the [hosted-agent deployment guide](https://learn.microsoft.com/azure/foundry/agents/how-to/deploy-hosted-agent).
Deployment and policy assignment modify cloud resources and may incur costs.
The local user secrets configured above are not automatically copied to the
azd environment. Once the intended existing azd environment and registry are
configured, set these nonsecret bindings and deploy from this demo directory:

```powershell
azd env set AZURE_AI_PROJECT_ENDPOINT (Read-Host 'Foundry project endpoint')
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME (Read-Host 'Model deployment name')
azd deploy better-together-guardrails
```

Confirm the deploy target and permissions before running these commands.
Assign the network policy separately in Foundry. Keep the same agent image for
both versions, select the intended version explicitly, and start a fresh
conversation and hosted session after changing the policy.

Set the policy to enforce the intended deny for
`brunocapuano.wordpress.com` and allow `devblogs.microsoft.com` if the default
is deny. Prefer exact hosts over broad wildcards. The tools use fixed URLs and
do not follow redirects.

## Expected result

In the Foundry playground, ask both versions:

```text
Show me the latest blog post from Bruno Capuano from his personal blog and from the .NET Blog.
```

Without the policy, both feeds should be available. With the policy, the
personal feed should be denied while the .NET Blog remains available. Check
the actual tool calls, then ask for the .NET Blog alone as a positive control.
Responses vary. A 403 alone does not establish why a request failed: inspect
the network egress decision trace, including destination, decision and rule,
before attributing it to a guardrail. If no decision trace is available, report
the observed result without claiming verified policy attribution.

## Cleanup

Stop the local host with Ctrl+C. Remove local bindings with
`dotnet user-secrets clear --project .\ApplicationGuardrails.csproj` and unset
any environment variables you set. Remove only cloud resources or policies
created for this demo after confirming they are not shared; do not delete
pre-existing projects, models, registries or policies.

## Resources

- [Foundry hosted agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
- [Episode guide](../../README.md)
