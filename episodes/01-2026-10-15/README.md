# Episode 1: .NET + Foundry, Better together

**Date:** October 15, 2026, 12:00-1:00 PM EDT (16:00-17:00 UTC).

**Event and registration:** [Microsoft Reactor](https://developer.microsoft.com/en-us/reactor/events/27630/)

## Overview

Explore .NET 10 and Microsoft Foundry through progressively richer native decision examples.
Start with a single bounded choice, use decisions in realistic review workflows,
then watch an original console invader-defense game.
Compare manual play, local Ollama, and Foundry native bounded decisions through the
same `IDecisionClient` API: text snapshots, four legal actions, probability
distributions, and confidence rather than generated chat JSON. An explicitly
labeled offline replay supports credential-free rehearsal.

## Recording

The recording link will be added after the episode.

## Demos and resources

Recommended learning order (folder numbers now match the walkthrough):

1. [Hello Decisions](demos/01-hello-decisions/README.md): define allowed choices and compare the community SDK with native REST.
2. [Support inbox triage](demos/02-support-triage/README.md): Choice, Score, Assess, and application-owned review policy using synthetic tickets.
3. [Answer-quality review](demos/03-answer-review/README.md): model-assisted review, not automatic approval.
4. [Decision Space Game](demos/04-decision-space-game/README.md): apply validated decisions in a running simulation.

The demo folders were renumbered before the broadcast so the game is last.
Project names and user-secrets IDs are unchanged; existing configuration still
works. Update any local working-directory shortcuts to the new paths.

### Configure all samples

Requires PowerShell 7 and the .NET 10 SDK. From this episode directory, before
recording or screen sharing:

```powershell
pwsh -File .\Setup-UserSecrets.ps1 -ValidateOnly
pwsh -File .\Setup-UserSecrets.ps1
```

[Setup-UserSecrets.ps1](Setup-UserSecrets.ps1) discovers runnable sample projects,
prompts once for the Foundry resource root and deployment name, and imports
configuration into each project's separate user-secrets store. Choose Azure CLI
identity (the default) or enter a key through hidden input. Identity mode removes
any stored `Foundry:ApiKey` so a stale key cannot override identity authentication.
Run `az login` separately and ensure your identity has deployment access.

Optional Ollama prompts configure only the game; they do not install Ollama or
download a model. Skipping Ollama leaves existing local settings unchanged.
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

- [Decision Space Game: code, setup, controls, tests, and validation](demos/04-decision-space-game/README.md)
  (.NET 10, ElBruno.AI.Decisions 0.6.2, manual/Ollama/Foundry/offline replay).
- [ElBruno.AI.Decisions](https://github.com/elbruno/ElBruno.AI.Decisions)
- [Microsoft SpaceAINet reference](https://github.com/microsoft/Generative-AI-for-beginners-dotnet/tree/main/samples/AppsWithGenAI/SpaceAINet)

[Back to the series](../../README.md)
