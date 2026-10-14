# Episode 1: .NET + Foundry, Better together

**Date:** October 15, 2026, 12:00-1:00 PM EDT (16:00-17:00 UTC).

**Event and registration:** [Microsoft Reactor](https://developer.microsoft.com/en-us/reactor/events/27630/)

## Overview

Explore .NET 10 and Microsoft Foundry through progressively richer native decision examples.
Start with one bounded choice in Foundry, repeat the same example locally with
Ollama, use decisions in realistic review workflows, then watch a console game.
The samples use the community `ElBruno.AI.Decisions` SDK, not an official Microsoft
SDK. Typed labels, scores, probabilities, and confidence replace generated chat
JSON; they are not guarantees of correctness.

## Recording

The recording link will be added after the episode.

## Demos and resources

Recommended learning order (folder numbers now match the walkthrough):

1. [Hello Decisions](demos/01-hello-decisions/README.md): one console app, one prompt, one decision using the community SDK.
2. [Hello Decisions with Ollama](demos/02-hello-decisions-ollama/README.md): the same prompt, labels, and output with a local native decision client.
3. [Same question, two models](demos/03-compare-decisions/README.md): MEAI chat versus native decision SDK, elapsed time and SDK-exposed usage.
4. [Support inbox triage](demos/04-support-triage/README.md): Choice, Score, Assess, and application-owned review policy for one synthetic ticket.
5. [Answer-quality review](demos/05-answer-review/README.md): model-assisted review, not automatic approval.
6. [Decision Lab](demos/06-decision-lab/README.md): Blazor, parallel repeated questions, live usage totals, cancellation, and Aspire GenAI traces.
7. [Decision Space Game](demos/07-decision-space-game/README.md): apply decisions in a running simulation.

The demo folders were renumbered before the broadcast so the game is last.
Existing project names and user-secrets IDs are unchanged; the new Ollama sample
has its own ID. Update local working-directory shortcuts to the new paths.
Each introductory console sample has one commented `Program.cs`, no command-line options,
and runs with `dotnet run`. Change the prompt directly in source. The game has a
similarly small entry point, with the simulation/rendering kept separately.
The larger Blazor demo uses Aspire and has its own run instructions.
Decision SDK 0.6.2 does not expose token usage: both comparison demos show that
usage as unavailable rather than zero. The feature request is
[elbruno/ElBruno.AI.Decisions#4](https://github.com/elbruno/ElBruno.AI.Decisions/issues/4).

### Configure all samples

Requires PowerShell 7 and the .NET 10 SDK. From this episode directory, before
recording or screen sharing:

```powershell
pwsh -File .\Setup-UserSecrets.ps1 -ValidateOnly
pwsh -File .\Setup-UserSecrets.ps1
```

[Setup-UserSecrets.ps1](Setup-UserSecrets.ps1) discovers runnable sample projects,
prompts once for the Foundry resource root and deployment name, and imports
configuration into the six Foundry apps' separate user-secrets stores. Seven
runnable apps are configured in total, including local-only demo 02.
The helper excludes Aspire AppHost/ServiceDefaults and test projects. It also
asks for a generative deployment on that same resource for demos 03 and 06. Choose Azure CLI
identity (the default) or enter a key through hidden input. Identity mode removes
any stored `Foundry:ApiKey` so a stale key cannot override identity authentication.
Run `az login` separately and ensure your identity has deployment access.

Optional Ollama prompts configure only demo 02, without Foundry values or keys.
They do not install Ollama or download a model. Skipping Ollama leaves that
project's existing settings unchanged. To run only demo 02, its README also
provides direct local configuration commands without requiring Foundry access.
Environment variables override user secrets, so remove conflicting `Foundry__*`
variables when changing authentication mode.

The script does not display supplied values, pass keys as process arguments,
write configuration into this repository, or run chargeable inference. Prompts
for non-key values are visible while you type, so do setup off-stream. .NET user
secrets are unencrypted local development storage, not a production vault. Imports
replace the named keys but preserve unrelated secrets. If setup fails midway,
earlier projects may already be updated; fix the reported prerequisite and rerun.
Use `dotnet user-secrets remove "<key>" --project "<project.csproj>"` for each
configured key when cleaning up. Never display `user-secrets list` on stream.

The setup script has non-writing tests:

```powershell
pwsh -File .\tests\Setup-UserSecrets.Tests.ps1
```

### Related resources

- [Decision Space Game: code, setup, controls, tests, and validation](demos/07-decision-space-game/README.md)
  (.NET 10, ElBruno.AI.Decisions 0.6.2, manual/Foundry/explicit offline replay).
- [ElBruno.AI.Decisions](https://github.com/elbruno/ElBruno.AI.Decisions)
- [Microsoft SpaceAINet reference](https://github.com/microsoft/Generative-AI-for-beginners-dotnet/tree/main/samples/AppsWithGenAI/SpaceAINet)

[Back to the series](../../README.md)
