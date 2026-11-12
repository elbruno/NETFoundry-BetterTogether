# Wikipedia agent: Foundry blocklist observer

This local Microsoft Agent Framework console demonstrates model-deployment
content filtering. `WikipediaDemoAgent` uses `AzureCliCredential` and asks a
Foundry model directly. It does not use MCP or filter words locally. A
read-only HTTP observer reports identifiable service filtering metadata,
including an HTTP 200 completion with `finish_reason: content_filter`.

## Prerequisites and versions

Use .NET SDK 11.0.100, PowerShell 7, Azure CLI, an existing Foundry project,
and an Azure OpenAI model deployment. Direct packages: Microsoft.Agents.AI.Foundry
1.28.0-preview.261106.1, Microsoft.Agents.AI.OpenAI 1.28.0, Azure.Identity
1.21.0, and Microsoft.Extensions.Configuration 11.0.0. The lockfile pins
transitive packages. Model calls and additional deployments can incur charges.

## Configure the service

1. In Foundry, create a custom blocklist named `Blocklist-Wikipedia` with the
   exact term `Wikipedia`, and add it as a blocking prompt and/or completion
   blocklist to a content filter. New entries can take several minutes to
   propagate.
2. Associate the filter with a **dedicated** Azure OpenAI model deployment.
   Filters apply to the model deployment and all its consumers, not just this
   agent. Do not modify a shared deployment without authorization.
3. Run the episode's `..\..\setup.ps1` and choose that deployment for the
   blocklist test. The script saves nonsecret `FOUNDRY_PROJECT_ENDPOINT` and
   `BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME` in local user secrets. This
   blocklist-specific name is separate from the general model setting, and
   setup rejects using the same deployment for both. Environment variables
   override saved settings.

The agent instructions themselves contain `Wikipedia`. A prompt-side
blocklist may therefore reject even the normal control questions below.
Use a completion-only test deployment or a neutral direct model call to isolate
the behavior, rather than assuming a control request will pass.

## Run

From this directory:

```powershell
az login
dotnet restore .\WikipediaBlocklist.csproj --locked-mode
dotnet run --project .\WikipediaBlocklist.csproj --no-restore
```

The console shows the agent name and deployment. Enter a public prompt and
inspect the response; `/exit` quits. Identifiable prompt or completion filter
signals are labeled, including custom-blocklist evidence when present.
Authentication and other service failures are reported separately. A 404
means the named deployment may not exist in this project; it is not evidence
of a guardrail decision. An error without a filtering signal is not described
as a guardrail block.

## Try the scenarios

1. Normal control: `Tell me a fun fact about C#.` It should succeed when
   neither the instructions nor the response trigger the configured filter.
2. Prompt: `Tell me about Wikipedia.` With the prompt blocklist enabled,
   Foundry should reject it and return identifiable filtering metadata.
3. Completion: `Explain how volunteers collaborate to create and maintain free reference articles online.`
   With the completion blocklist enabled, a response mentioning the blocked
   term may be filtered. The model might use different wording, so this is
   probabilistic.
4. Recovery: `Tell me a fun fact about C# generics.` The console remains
   available after an error; this request succeeds only if its full input and
   output do not trigger the configured policy.

### Direct completion-only probe

Use a dedicated deployment where the exact term is blocked **only on
completion**, not on the prompt. The request asks the model for that exact
term, without the agent's Wikipedia-specific instructions. This verifies the
service response directly and does not change any policy:

```powershell
$projectEndpoint = Read-Host 'Foundry project endpoint'
$deployment = Read-Host 'Completion-only model deployment name'
$token = az account get-access-token --resource 'https://ai.azure.com' --query accessToken --output tsv
if ($LASTEXITCODE -ne 0 -or -not $token) { throw 'Azure CLI token acquisition failed.' }
try {
    $body = @{
        model = $deployment
        messages = @(@{ role = 'user'; content = 'Reply with the exact single word Wikipedia, and nothing else.' })
    } | ConvertTo-Json -Depth 5
    $response = Invoke-RestMethod -Method Post `
        -Uri "$($projectEndpoint.TrimEnd('/'))/openai/v1/chat/completions" `
        -Headers @{ Authorization = "Bearer $token" } `
        -ContentType 'application/json' -Body $body
    $response | ConvertTo-Json -Depth 20
} finally {
    Remove-Variable token -ErrorAction SilentlyContinue
}
```

Look for `choices[].finish_reason` equal to `content_filter` and, when
available, `content_filter_results.custom_blocklists` with `filtered: true`.
If the model does not attempt the requested word, or those service signals are
absent, the result does not prove completion blocking. The command keeps the
short-lived token in memory and does not print it.

## Cleanup

Run `dotnet user-secrets clear --project .\WikipediaBlocklist.csproj`, unset
`BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME` if you set it, and remove only the dedicated test policy,
blocklist, and deployment if you created them and no longer need them. Do not
delete or modify shared resources.

## Resources

- [Foundry custom blocklists](https://learn.microsoft.com/azure/foundry/openai/how-to/use-blocklists)
- [Microsoft Agent Framework quickstart](https://learn.microsoft.com/agent-framework/tutorials/quick-start?pivots=programming-language-csharp)
- [Episode guide](../../README.md)
