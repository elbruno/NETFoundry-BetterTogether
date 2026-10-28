# Demo 01: Call a Foundry decision model from C#

This first step is a plain C# console app. It sends a synthetic support ticket
directly to an existing decision-model deployment in Microsoft Foundry and
prints the selected label, confidence, probability distribution, and
provider-reported usage. It does not create an agent or call a chat model.

The sample uses `ElBruno.AI.Decisions.Foundry` 0.6.3 to make the native
decision-model call. The same ticket, question, labels, endpoint, and deployment
are used in the next two demos.

`FoundryDecisionClient` accepts its options directly and owns its HTTP transport.
No separate `HttpClient` is needed. Use a resource-root endpoint; project URLs
are not supported by this sample's package version.

## Tested versions

| Component | Version |
| --- | --- |
| .NET SDK | 10.0.403 |
| Azure.Identity | 1.17.0 |
| ElBruno.AI.Decisions.Foundry | 0.6.3 |

The lock file pins the resolved dependency graph. This sample was built for
Windows PowerShell.

## Prerequisites

- .NET SDK 10.0.403 or a later 10.0 patch.
- Azure CLI signed in with an identity authorized to call the existing decision
  model.
- The Foundry resource root URL and the name of its existing decision-model
  deployment.

Do not use an API key. The app uses `AzureCliCredential` for local development.

## Run

From this demo directory:

```powershell
dotnet restore .\src\DecisionConsole --locked-mode
dotnet build .\src\DecisionConsole -c Release --no-restore

$env:Decision__Endpoint = 'https://<resource>.services.ai.azure.com/'
$env:Decision__Deployment = '<decision-deployment>'
dotnet run --project .\src\DecisionConsole -c Release --no-launch-profile
```

The default synthetic ticket is:

```text
I was charged twice for my subscription.
```

To try another synthetic ticket, edit the `ticket` constant in `Program.cs`.
The commented alternative uses `The sign-in page rejects my MFA code.` Run
the app again to rebuild the changed source:

```powershell
dotnet run --project .\src\DecisionConsole -c Release --no-launch-profile
```

The output is a JSON object with the model's decision values. Inference is
nondeterministic, so the exact label and numeric values are not prescribed.
Provider-reported usage may be absent.
Keep the ticket short and synthetic. The later agent stages explicitly enforce
their own input contract.

## What this demo establishes

- A C# app can call the decision model without putting it behind an agent.
- The structured result is available to application code.
- Authentication and model authorization are separate requirements.

This sample only prints a suggestion. It does not update a ticket or prove model
accuracy. Local inference can incur Foundry charges. Use synthetic text only.

Remove the local configuration when finished:

```powershell
Remove-Item Env:Decision__Endpoint, Env:Decision__Deployment -ErrorAction SilentlyContinue
```

Continue to [Demo 02: Run the decision inside a local agent](../02-local-agent/README.md).
