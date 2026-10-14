# Decision Space Game

The finale: the same Foundry decision client from demo 01 now chooses actions in
a running console game. Start with **Program.cs**: create the client and open
the game. There are no command-line arguments.

The game still needs a simulation, renderer, and asynchronous action handling.
Those stay separate from the short teaching entry point. This is not a claim
that a playable game can be implemented with one inference call.

## Read on stream

1. `Program.cs`: external configuration, Foundry client, start game.
2. `Game.cs`: four allowed actions, instructions, compact state snapshot.
3. `DecisionDriver.cs`: `ChooseAsync`, then a legal and timely action.
4. `GameConsole.cs`: rendering and keyboard plumbing, only if needed.

The model chooses `move_left`, `move_right`, `fire`, or `stop` from text state,
not a screenshot. Application code retains collision, cooldown, bounds, and
stale-action checks because the simulation continues during inference.
Those are game rules, not extra scaffolding in the hello-world examples.

## Setup and run

- .NET SDK **10.0.401** (patch roll-forward), targeting .NET 10.
- **ElBruno.AI.Decisions.Foundry 0.6.2**, community SDK, not an official SDK.
  The **Ollama 0.6.2** package remains for offline provider-contract tests.
- Configuration packages **10.0.0**; complete dependency versions are locked.
- Existing Microsoft-Decision-1 deployment and authorized Azure CLI identity,
  or an optional API key supplied externally.
- A real terminal, preferably at least **100 columns by 42 rows**.

From this directory, configure off stream:

```powershell
pwsh -File ..\..\Setup-UserSecrets.ps1
dotnet restore DecisionSpaceGame.slnx --locked-mode
dotnet run
```

Settings: `Foundry:Endpoint`, `Foundry:Model` (deployment name), optional
`Foundry:ApiKey`. Existing user-secrets ID is unchanged:
`netfoundry-episode01-decision-space-game`. Environment variables override
user secrets. Do not display configuration values or identity diagnostics.

The game starts in Foundry mode, seed 42, with a 90-second limit, ten-second
request timeout, and five-second snapshot age limit. These visible teaching
tradeoffs are defined in `GameConsole.cs`, not a production prescription.

| Key | Action |
| --- | --- |
| M | Explicit manual control |
| A/Left, D/Right, Space, S | Move, fire, stop in manual mode |
| F | Return to Foundry control or retry a failed provider |
| R | Explicit OFFLINE scripted replay, NOT model inference |
| Q/Escape or Ctrl+C | Quit |

Ollama is demonstrated separately in [demo 02](../02-hello-decisions-ollama/README.md).
The game no longer exposes provider selection through command-line arguments or
an Ollama keyboard shortcut. It displays a board, score, lives, selected action,
latency, label probabilities, and separate provider confidence.

## Engine validation

```powershell
dotnet test DecisionSpaceGame.slnx -c Release
```

Tests use synthetic inputs and fake HTTP, plus an explicitly scripted headless
game loop. They need no live endpoint and incur no Foundry charges.
The game simulation/driver tests remain; obsolete CLI-parser tests were removed.
Physical keyboard/rendering and model play quality still require rehearsal.
The earlier live headless checks predate this simplified entry point.

## Failure, cost, and cleanup

Configuration exceptions surface normally. Provider failures during play remain
visible and pause AI control; choose M for manual play or F to retry. Never call
scripted replay live inference. Probabilities/confidence do not guarantee a
correct move or measured performance.

Each Foundry decision can incur Azure charges and consume quota. Q stops the
game and cancels pending work; a request already received by the service can
still be charged. No resources are created; do not delete shared deployments.
To remove only this project's settings:

```powershell
dotnet user-secrets clear --project .\DecisionSpaceGame.csproj
```

Remove any `Foundry__*` environment overrides as appropriate.

## Resources

- [Native decision API](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision)
- [Community SDK](https://github.com/elbruno/ElBruno.AI.Decisions)
- [Episode](../../README.md)
