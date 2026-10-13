# Decision Space Game

An original ASCII invader-defense game for **Episode 1: .NET + Foundry, Better together**.
Play yourself, use a local Ollama decision model, or let a Foundry deployment choose
from four bounded actions. One game, one simulation, different decision providers.

Unlike a chat agent that generates JSON prose and then parses it, this demo calls
`IDecisionClient.ChooseAsync(snapshot, instructions, options, cancellationToken)`
and receives a typed `ChoiceDecision`: `Choice`, `Probabilities`, and `Confidence`.
The JSON snapshot is **text**, not a screenshot: the shared SDK is text-only.

## Prerequisites and tested versions

- .NET SDK **10.0.401**, runtime **10.0.12**, targeting `net10.0`. `global.json`
  permits later patches of the 10.0.4xx feature band.
- NuGet: **ElBruno.AI.Decisions.Foundry 0.6.2** and
  **ElBruno.AI.Decisions.Ollama 0.6.2** (core **0.6.2** transitively).
- Configuration environment/user-secrets packages **10.0.0**;
  transitive **Azure.Identity 1.17.0**, **Azure.Core 1.49.0**.
- Tests: **Microsoft.NET.Test.Sdk 17.14.1**, **xunit 2.9.3**,
  **xunit.runner.visualstudio 3.1.4**.
- The two committed `packages.lock.json` files record the complete tested graph.
  The demo-local `NuGet.Config` uses public nuget.org, not a private feed.
- For interactive play: a real console, preferably **100 columns × 42 rows** or
  larger. Short windows clip the board; redirected input/output requires
  `--headless` or `--probe`. No Aspire or extra game framework.
- Foundry only: Azure CLI signed in, permission to invoke an **existing**
  Microsoft-Decision-1 deployment, and its resource endpoint/deployment name.
- Ollama only: **v0.35.0+**, running locally with an actual System One decision
  model such as `nimble`. Ordinary chat models do not implement this protocol.

## Build and credential-free rehearsal

From the repository root in PowerShell:

```powershell
Set-Location .\episodes\01-2026-10-15\demos\01-decision-space-game
dotnet --version
dotnet restore DecisionSpaceGame.slnx --locked-mode
dotnet build DecisionSpaceGame.slnx -c Release --no-restore
dotnet test DecisionSpaceGame.slnx -c Release --no-build
dotnet run -c Release --no-build -- --help
dotnet run -c Release --no-build -- --provider manual
```

| Key | Behavior |
| --- | --- |
| Left / A, Right / D | Move (manual mode only) |
| Space | Fire once (manual mode only) |
| S | Stop (manual mode only) |
| M | Explicitly select manual control, including after a provider failure |
| O / F | Select Ollama / Foundry; reselect to retry a failed provider |
| R | Select **OFFLINE scripted replay — NOT a model** |
| Q / Escape / Ctrl+C | Quit and cancel outstanding inference |

The game runs for 60 seconds by default, or until victory/game over/quit.
Extend it with `--seconds 180`. You are `^`, invaders are `W`, your shots are `|`,
and incoming hostile shots are `!`. Destroy all invaders; avoid losing three lives.

No credentials or services are needed for these reproducible smoke commands:

```powershell
dotnet run -c Release --no-build -- --provider manual --headless --seconds 2
dotnet run -c Release --no-build -- --provider replay --headless --seconds 3 --seed 42
dotnet run -c Release --no-build -- --provider replay --probe
```

Expected: manual advances about 40 ticks with no automatic actions. Replay clearly
labels itself `OFFLINE scripted actions (NOT a model)`, applies scripted choices,
prints latency/distributions, and exits with a `FINAL` score/lives summary.
Replay is a scripted rehearsal, **not a captured live model run**; its synthetic
100% probabilities/confidence say nothing about model quality. Actual wall-clock
tick counts and replay action-application ticks can vary slightly with scheduling.

## Configure Foundry (no API key required with Azure CLI)

```powershell
az login
dotnet user-secrets set "Foundry:Endpoint" "https://<resource>.services.ai.azure.com"
dotnet user-secrets set "Foundry:Model" "<deployment-name>"
dotnet run -c Release --no-build -- --provider foundry --probe --timeout-seconds 20
dotnet run -c Release --no-build -- --provider foundry --headless --seconds 10 --timeout-seconds 10 --max-age-seconds 5
dotnet run -c Release --no-build -- --provider foundry --seconds 60 --timeout-seconds 10 --max-age-seconds 5
```

Replace placeholders with **your existing deployment**. `Foundry:Model` is the
**deployment name**, not necessarily the catalog name `Microsoft-Decision-1`.
The SDK appends `/providers/microsoft/v1/systemone` to a resource root; a full
invocation URL is also supported. Do not use a project endpoint or a chat route.
Without a key the package uses `AzureCliCredential`, not a broad default credential chain.
A supplied `Foundry:ApiKey` takes precedence over Entra authentication.

Only if key authentication is required:

```powershell
dotnet user-secrets set "Foundry:ApiKey" "<your-api-key>"
```

Do not paste a real key into source, screenshots, logs, or shared terminal history.
User secrets are development storage outside this repository, **not encrypted**.
The published UserSecretsId is `netfoundry-episode01-decision-space-game`.
No credentials, real resource IDs, or resource-specific defaults are in this demo.
HTTP 401/403 indicates authentication/authorization trouble; the game displays the
error and suspends AI. It does not switch providers or invent a successful result.

## Configure Ollama

Local System One requires **both** a compatible server (**Ollama v0.35.0+**) and
the native protocol client (**ElBruno.AI.Decisions.Ollama 0.6.1+**; this demo pins
and tests **0.6.2**). Package **0.6.0** used a chat-logprob approach and is not
suitable for this native demo. A newer .NET SDK alone does not add System One
support to an older Ollama server or a general-purpose chat model.

Install/start Ollama and obtain a compatible decision model separately, if desired:

```powershell
ollama --version
ollama pull nimble
dotnet user-secrets set "Ollama:Endpoint" "http://localhost:11434/"
dotnet user-secrets set "Ollama:Model" "nimble"
dotnet run -c Release --no-build -- --provider ollama --probe --timeout-seconds 60
dotnet run -c Release --no-build -- --provider ollama --seconds 60 --timeout-seconds 60 --max-age-seconds 5
```

The endpoint uses `/v1/systemone`. Model installation may be a large download;
the demo itself never installs Ollama or pulls models. Cold model loading can
exceed the five-second default timeout. Test a probe first, then tune timeout and
maximum snapshot age deliberately. Increasing timeout does **not** disable stale
decision rejection. The library's live Ollama compatibility remains unverified
on this development machine; see validation below.

### Configuration keys and environment equivalents

Environment variables override user secrets. No configuration file with credentials
is read or needed. Environment values in a PowerShell session last until removed
or the session exits.

| User-secret key | Environment variable | Required/default |
| --- | --- | --- |
| `Foundry:Endpoint` | `Foundry__Endpoint` | Required for Foundry, HTTPS |
| `Foundry:Model` | `Foundry__Model` | Required for Foundry, deployment name |
| `Foundry:ApiKey` | `Foundry__ApiKey` | Optional, otherwise Azure CLI identity |
| `Ollama:Endpoint` | `Ollama__Endpoint` | `http://localhost:11434/` |
| `Ollama:Model` | `Ollama__Model` | Required for Ollama |

For example, set `$env:Foundry__Model = '<deployment-name>'` rather than a secret
command. CLI settings are shown by `--help`: provider, bounded wall-clock seconds,
integer seed, request timeout, maximum snapshot age, headless, and probe.
Unknown flags, missing values, invalid providers, non-finite numbers, and
out-of-range durations are errors. Exit codes: **0** completed run/probe,
**1** provider failure/illegal probe/no decisions applied in an AI headless run,
**2** invalid CLI/configuration/terminal usage.

## Decision and simulation semantics

- The board is 32×20, origin top-left, x increases right, y increases down.
  The ship remains at y=18. Simulation steps are fixed at **50ms**.
- `move_left` / `move_right`: one column per tick for **four ticks (200ms)**,
  clamped to the board. No firing during movement.
- `fire`: stay in place, create **one** upward shot if cooldown is zero.
  Cooldown is six ticks (300ms); fire is not repeated by a held AI action.
- `stop`: stop movement, do not fire. An expired movement lease also stops.
- Friendly shots move up one row per tick; hostile shots down one row every
  two ticks. Enemies move every ten ticks, bouncing and descending at an edge;
  a seeded enemy fires every twenty ticks.
- Rendering/input run separately from inference. A worker receives an immutable
  JSON snapshot; only the main loop mutates game state. Keyboard provider switches
  and quit remain available during inference. Manual moves require selecting M.
- At most **one request in flight**, even across provider switches. Switch/quit
  cancels the old request; an old-provider completion can never control the ship.
  A request timeout is visible immediately; a cancellation-ignoring request keeps
  the slot occupied until it settles, rather than starting overlapping requests.
- New requests start no faster than once per six ticks. Results are discarded if
  their snapshot exceeds `--max-age-seconds` (default 1.5), the game has ended,
  labels/distributions are invalid, a move points outside the board, or fire is
  on cooldown. There is no probability-based relabeling, hidden retry, or fallback.
- Provider failures stop AI action and remain visible. Press M for manual fallback
  or explicitly reselect a provider to retry. Shutdown awaits cancellation before
  disposing HTTP clients.
- The HUD/output shows provider/model, score/lives, latency, exact selected label,
  **selected-label probability separately from provider confidence**, and all
  four probabilities. Confidence is **not calibrated correctness**. Ollama's
  reported concentration confidence and Foundry confidence need not be identical.

`--probe` always sends the **initial seed-defined tick-0 state once**, prints that
snapshot and decision, and exits. It does not play or adjust the snapshot to make a
model look successful. Use the same seed/provider config to compare distributions;
live model results are not promised deterministic. A seeded simulation with the
same tick-by-tick actions is deterministic; asynchronous live gameplay is not.

## Validation (October 13, 2026)

- Locked restore and Release build passed; **44 tests**, none skipped:
  bounds, action leases/fire cooldown/collisions/seeded state, keyboard/action
  mapping, stale/illegal decisions, timeout/cooperative cancellation, provider
  switching with late completions/no overlap, explicit fallback, CLI validation,
  and **synthetic HTTP contract tests** for both published provider packages.
- Manual two-second headless smoke passed (39 ticks, score 0, three lives).
  Offline three-second replay passed (59 ticks, ten applied decisions, score 10,
  three lives); fixed replay probe passed. Timing-dependent counts can vary.
- Live Foundry fixed-state probe passed with **existing Azure CLI sign-in, no
  API key**: probes chose `fire`, selected-label probability approximately
  **97.2–97.5%**, confidence **96.3–96.7%**, latency **1.5–3.7s**.
  A ten-second headless live run with timeout 10s and
  max age 5s applied **seven** decisions, score **20**, three lives; typical
  subsequent decisions took about **1.2s**. These are observations, not a benchmark
  or a promise of gameplay quality/calibration.
- **No live Ollama success claimed**: no executable on PATH or the standard
  per-user installation location; a localhost probe timed out with an explicit
  error. Native HTTP fixture tests validate wiring, not a real installed model.
- **Physical interactive terminal play was not verified in the automated host**:
  terminal UI automation was blocked by host policy. Keyboard mapping, nonblocking
  simulation/late-request handling, and headless runs were tested; rehearse manual
  movement, shooting, F/O/R/M switches, resizing, and quitting in your terminal
  before presenting.

## Cost, cleanup, limitations, and credits

Foundry inference is **billable** under your deployment/subscription pricing.
A 60-second game can issue many requests; use a short probe/bounded smoke first.
This demo creates **no Azure resources**. Do not delete a shared deployment,
resource group, or existing resource to clean up.

Remove only this demo's local configuration when finished:

```powershell
dotnet user-secrets remove "Foundry:Endpoint"
dotnet user-secrets remove "Foundry:Model"
dotnet user-secrets remove "Foundry:ApiKey"
dotnet user-secrets remove "Ollama:Endpoint"
dotnet user-secrets remove "Ollama:Model"
Remove-Item Env:\Foundry__Endpoint, Env:\Foundry__Model, Env:\Foundry__ApiKey, Env:\Ollama__Endpoint, Env:\Ollama__Model -ErrorAction SilentlyContinue
```

Stop only an Ollama server you started for this rehearsal; do not remove shared
models/services. Manual/replay incur no inference cost. The game is deliberately
small: no image API, training, chat parsing, benchmark harness, or production
resilience framework. Slow decisions can be tactically poor/stale; the single
action choice cannot move and shoot simultaneously.

- [ElBruno.AI.Decisions README and provider setup](https://github.com/elbruno/ElBruno.AI.Decisions)
  — community native decision abstraction; early-access 0.6.2.
- [Microsoft SpaceAINet reference](https://github.com/microsoft/Generative-AI-for-beginners-dotnet/tree/main/samples/AppsWithGenAI/SpaceAINet)
  — inspiration for contrasting an `IChatClient`/JSON-prose controller with native
  bounded decisions. This game's code/art are original, not copied from that game.
- [Microsoft-Decision-1 introduction](https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/introducing-microsoft-decision-1-in-microsoft-foundry-for-decision-and-classific/4562742)
- [Ollama decision guide](https://docs.ollama.com/capabilities/decision)
  and [System One API](https://docs.ollama.com/api/systemone).

[Back to Episode 1](../../README.md)
