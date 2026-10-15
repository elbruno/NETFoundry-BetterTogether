# Hello Decisions with Ollama

The same message, choices, question, and output as [demo 01](../01-hello-decisions/README.md),
using a local `OllamaDecisionClient` instead of Foundry. Open **Program.cs** and
read top to bottom: create client, define prompt, call `ChooseAsync`, print result.
There are no command-line arguments or helper classes. Try the two commented
alternative messages by editing `prompt`.

## Prerequisites

- .NET SDK **10.0.401** (patch roll-forward in `global.json`).
- **Ollama 0.35.0+**, running locally with a native System One decision model.
  An ordinary chat model is not a substitute.
- **ElBruno.AI.Decisions.Ollama 0.6.3**, a community SDK, and configuration
  packages **10.0.0**. The lock file records the restored versions.

A live run was verified with **Ollama 0.40.4** and **nimble:latest**
(9B, Q8_0). The billing message selected `billing`. This is a smoke test,
not a benchmark or guarantee for other inputs or hardware. Allow for model
loading/warm-up before presenting.

Install/start Ollama separately and download a compatible decision model, for
example `ollama pull nimble`. Model downloads require disk space and suitable
hardware. The setup script does not install software or download models.

## Configure and run

From this directory, run the episode helper before screen sharing and choose
Ollama configuration:

```powershell
pwsh -File ..\..\Setup-UserSecrets.ps1
dotnet restore --locked-mode
dotnet run
```

This sample reads `Ollama:Endpoint` (typically `http://localhost:11434/`) and
`Ollama:Model` from its separate user-secrets store,
`netfoundry-episode01-hello-decisions-ollama`. `Ollama__Endpoint` and
`Ollama__Model` environment variables override these settings.
No API key or Foundry access is needed to run this demo.

To configure only this local sample without the event helper:

```powershell
dotnet user-secrets set "Ollama:Endpoint" "http://localhost:11434/"
dotnet user-secrets set "Ollama:Model" "nimble"
```

Output is the message, selected team, each option's probability, and separate
provider confidence. Results can differ from Foundry and between runs. Neither
probability nor confidence guarantees correctness. There is no chat response,
generated rationale, or real ticket routing.

## Errors, costs, and cleanup

SDK 0.6.3 prints actual nullable usage counters. A live nimble rehearsal reported
210 input tokens and **0 output tokens**, with total omitted. Zero is a reported
value, not missing telemetry. Counts depend on the provider and are not costs.

This minimal sample lets SDK/configuration exceptions surface normally.
Check server availability, native API/model compatibility, and local settings
off stream if it fails. There is no fake fallback.

If Ollama returns HTTP 400 saying the model was downloaded in a format that is
no longer supported, refresh it with `ollama pull nimble:latest` (or your configured
model name), then rerun the demo. This can download the model again and updates
the local model shared by demos 02, 07, and 08.

Local inference makes no Foundry request and incurs no Azure inference charge.
It uses local compute, memory, and electricity. Stop your local server when no
longer needed. Remove only models you intentionally downloaded, using
`ollama rm nimble` if that model is no longer needed by other work.
Clear this project's settings with:

```powershell
dotnet user-secrets clear --project .\HelloDecisionsOllama.csproj
```

Remove any `Ollama__*` environment variables you set. A successful build does not
prove that your server/model supports the native decision API; rehearse a real run.

## Resources

- [Ollama decision capabilities](https://docs.ollama.com/capabilities/decision)
- [Ollama SystemOne API](https://docs.ollama.com/api/systemone)
- [Episode](../../README.md)
