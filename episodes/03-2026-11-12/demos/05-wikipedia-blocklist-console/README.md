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

## Compare the playground before and after a blocklist

1. In Foundry, deploy a supported Azure OpenAI chat model as a **dedicated**
   model deployment, for example `gpt-Blocklist-Wikipedia`. Select a model,
   deployment type, and capacity available to your project, then wait for the
   deployment to be ready. A model deployment can incur charges. Do not modify
   a shared deployment.
2. Open the Foundry chat playground, select the new deployment, and start a
   fresh chat. Use neutral system instructions that do not contain the word
   `Wikipedia`, and verify that no Wikipedia blocklist is already applied to
   the deployment. Ask these questions one at a time:

   ```text
   What is Wikipedia, and who founded it?
   What is Wikipedia used for?
   ```

   Expect ordinary answers for the baseline, but inspect the actual results.
   The standard safety filters still apply.
3. Under Foundry guardrails, reuse an existing blocklist such as
   `Blocklist-Wikipedia`, or create one, and verify that it includes the exact
   term `Wikipedia`. The blocklist and the model deployment are separate
   resources, and do not need matching names.
4. Create a dedicated content filter with that list enabled as a **blocking
   prompt/input blocklist**, then associate the filter with the new model
   deployment. Do not change a filter used by unrelated applications. A
   blocklist is applied through a content filter, not directly to the model.
5. Allow time for the change to propagate, start a fresh playground chat on the
   same deployment, and ask the same two questions again. A prompt blocklist
   should deny them; show the service or playground filtering signal instead
   of assuming success. Try `What is a generic type in C#?` as a neutral
   control, subject to all configured filters.

If a question is not denied, verify the selected deployment, filter
association, input blocklist, and exact term. New blocklist terms can take
several minutes to propagate. A 404, authentication failure, or another
unrelated service error is not proof of filtering.

## Configure the C# observer

Run the episode's `..\..\setup.ps1` from this demo directory and enter the
**model deployment name** for the blocklist test. The script saves nonsecret
`FOUNDRY_PROJECT_ENDPOINT` and `BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME` in
local user secrets. The blocklist deployment must differ from the general
episode model deployment.

If setup was already run, update just this demo's saved deployment name after
the deployment is ready. Substitute your actual deployment name if different:

```powershell
dotnet user-secrets set BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME gpt-Blocklist-Wikipedia --project .\WikipediaBlocklist.csproj
..\..\setup.ps1 -Validate
```

Environment variables override saved settings. Validation checks configuration
format and the separate deployment name; it does not check Azure access,
deployment existence, or filter assignment. The script does not create cloud
resources or apply policies.

The observer uses neutral agent instructions that do not contain the blocked
term, so the instructions alone do not trigger this prompt blocklist. Other
configured filters and generated output can still affect a neutral control.

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

1. Normal control: `What is a generic type in C#?` It should normally succeed
   with the prompt-only Wikipedia blocklist and otherwise nonblocking policies.
2. Prompt: `What is Wikipedia, and who founded it?` With the prompt blocklist
   enabled, Foundry should reject it and return identifiable filtering metadata.
3. Repeat: `What is Wikipedia used for?` This matches the second playground
   prompt and should also be blocked by the prompt list.
4. Recovery: `Tell me a fun fact about C# generics.` The console remains
   available after an error; this request succeeds only if its full input and
   output do not trigger the configured policies.

To explore completion filtering separately, configure an output blocklist and
ask `Explain how volunteers collaborate to create and maintain free reference
articles online.` A response containing the blocked term may be filtered, but
the model might use different wording, so the outcome is probabilistic.

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
`BLOCKLIST_AZURE_OPENAI_DEPLOYMENT_NAME` if you set it, and remove only a
dedicated test policy, blocklist, or deployment that you created and no longer
need. Do not delete a reused blocklist or other shared resources.

## Resources

- [Foundry custom blocklists](https://learn.microsoft.com/azure/foundry/openai/how-to/use-blocklists)
- [Microsoft Agent Framework quickstart](https://learn.microsoft.com/agent-framework/tutorials/quick-start?pivots=programming-language-csharp)
- [Episode guide](../../README.md)
