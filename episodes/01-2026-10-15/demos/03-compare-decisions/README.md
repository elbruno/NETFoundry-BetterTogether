# Same question: generative model versus decision model

One support message, the same question and label descriptions, two Foundry
models. The generative model returns schema-constrained JSON containing one team
through `Microsoft.Extensions.AI.IChatClient`. Microsoft-Decision-1 returns a
native bounded choice through `ElBruno.AI.Decisions.Foundry`. The console prints
each actual choice, both providers' exposed token usage, and measured
client-call elapsed time.

## Read Program.cs

A configures one Foundry resource and two SDK clients. The OpenAI .NET
`ResponsesClient` is adapted to MEAI with `AsIChatClient`; the decision SDK owns
the SystemOne call. B makes one logical call to each SDK. C prints actual
choices and telemetry. There is no raw HTTP, CLI argument, framework helper, or
custom catch. Edit the commented alternative prompts directly in source.

The generative response's `ChatResponse.Usage` is populated by the OpenAI MEAI
adapter when the service provides usage. Null values display **not reported**.
The decision SDK's `ChoiceDecision.Usage` now exposes nullable `InputTokens`,
`OutputTokens`, `TotalTokens`, `ReasoningTokens`, and `CachedTokens` in **0.6.3**.
The feature shipped in the closed
[elbruno/ElBruno.AI.Decisions#4](https://github.com/elbruno/ElBruno.AI.Decisions/issues/4).
Counts come through the SDK, not raw HTTP interception. Missing remains
**not reported**, reported zero remains zero, and totals are never inferred.

## Prerequisites and run

- .NET SDK **10.0.401**, targeting .NET 10, latest-patch roll-forward.
- Direct package versions: `Azure.Identity` **1.17.0**,
  `ElBruno.AI.Decisions.Foundry` **0.6.3**,
  `Microsoft.Extensions.AI.OpenAI` **10.11.0**, and both
  `Microsoft.Extensions.Configuration.UserSecrets` and
  `Microsoft.Extensions.Configuration.EnvironmentVariables` **10.0.0**.
  The lock file records exact transitive dependencies, including MEAI
  abstractions **10.11.0** and OpenAI .NET **2.14.0**.
- Two deployments on the same Foundry resource: Microsoft-Decision-1 and a
  generative model supporting the Responses API and strict JSON schema. Live
  tested with **gpt-6.1-sol** and Microsoft-Decision-1.
- Azure CLI authentication (`az login` off stream) or a Foundry API key. The
  OpenAI .NET Responses API is currently marked preview by its SDK diagnostic.

From this directory, configure off stream, then run:

```powershell
pwsh -File ..\..\Setup-UserSecrets.ps1
dotnet restore --locked-mode
dotnet run
```

The user-secrets store is `netfoundry-episode01-decision-comparison` and
contains `Foundry:Endpoint` (resource root), `Foundry:Model` (decision
deployment), `Foundry:GenerativeModel` (generative deployment), and optional
`Foundry:ApiKey`. Environment variables such as `Foundry__GenerativeModel`
override user secrets. The helper configures seven runnable apps; it does not create
resources or run inference.

For a process-only configuration (no user-secrets changes), set
`Foundry__Endpoint`, `Foundry__Model`, and `Foundry__GenerativeModel` in the
PowerShell process and leave `Foundry__ApiKey` unset to use Azure CLI identity.

## Interpret the comparison fairly

- A successful live billing example selected `billing` with both models.
  Exact labels, usage, and timings may change.
- Input includes equivalent task information, but each SDK serializes it
  differently. Tokenizers and server-side formatting differ too.
- The generative model gets a strict enum schema and no request for prose.
  Its output budget is 2,048 tokens, not a target or preallocated charge.
  Default reasoning behavior is unchanged. Any reported reasoning tokens are
  already included in output tokens. This is not tuned for minimum latency.
- Usage comes from both SDK responses. A live billing run reported GPT
  103 input / 14 output / 117 total, and Microsoft-Decision-1 67 input / 1 output
  with total/reasoning/cached not reported. These are observations, not expected
  counts. We do not relabel 67+1 as a service-reported total.
- Elapsed time surrounds each SDK call and includes any authentication,
  transport, and response processing performed within it. Calls run
  sequentially, generative first. There is no warm-up, order balancing,
  repetition, latency percentile, or accuracy evaluation.
- A single short example is **not a benchmark**. Token counts are not equal units
  of computation or cost across models. No price/savings claim is calculated.
- Decision probabilities are not comparable to a generative model's
  self-reported confidence, so the example does not ask the generative model to
  invent one.

This teaches API behavior, output shape, and observable usage, not that one model
always wins. Use generative models for generation/explanation; evaluate bounded
decision models on representative labeled data.

## Errors, charges, and cleanup

Configuration, SDK, and parsing exceptions surface. A refusal, incomplete
response, unsupported schema, or unsupported model is not a successful
classification. Do not diagnose credentials or display raw response bodies on
stream. There are no app-level retries, model substitutions, or fake fallbacks.

Each normal run makes **two logical SDK calls**. The SDK may retry transient
transport failures according to its defaults, and an interrupted/failed run may
already have incurred charges. Different model prices and deployment types
apply. No new Azure resources are created; never delete shared deployments.

```powershell
dotnet user-secrets clear --project .\DecisionComparison.csproj
```

Remove any process/environment overrides you set.

## Resources

- [Native decision API and usage](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision)
- [Responses API](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/responses)
- [Decision SDK usage feature, shipped in 0.6.3](https://github.com/elbruno/ElBruno.AI.Decisions/issues/4)
- [Episode](../../README.md)