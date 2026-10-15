# Platformer decisions

An optional graphical extension to [demo 07, Decision Space Game](../07-decision-space-game/README.md).
This sample starts in manual mode. When you choose a provider, Microsoft-Decision-1
or a compatible local Ollama decision model receives structured game state and
chooses one bounded movement/jump action. The application continues to own
physics, collisions, input, and scoring. The model receives no screenshot.

The sample is not an autonomous-play guarantee or a speed/cost benchmark. Foundry
Microsoft-Decision-1 and a local Ollama model are different models, not identical
weights in two locations. A quick defeat is not a win.

## Build and run

Requires the .NET 10 SDK **10.0.401** (the included `global.json` allows only
later patches in that feature band) and Windows desktop support for MonoGame
DesktopGL. The content builder requires the pinned local `dotnet-mgcb` tool.

From this directory:

```powershell
dotnet tool restore
dotnet restore .\PlatformerDecisions.slnx --locked-mode
dotnet build .\PlatformerDecisions.slnx -c Release --no-restore
dotnet test .\PlatformerDecisions.slnx -c Release --no-restore
dotnet run
```

The runnable `Platformer2D.csproj` is in this directory, so plain `dotnet run`
launches manual play. The solution groups the game, Core library and tests for
IDE use and solution-wide checks; a solution alone is not a runnable project.
For the prebuilt Release version, use `dotnet run -c Release --no-build`.

The four credential-free contract tests run by default. The graphics-backed
tests are opt-in and require a desktop and Release content already built:

```powershell
$env:PLATFORMER_DESKTOP_TESTS = '1'
dotnet test .\tests -c Release --no-restore
Remove-Item Env:\PLATFORMER_DESKTOP_TESTS
```

This additionally exercises real level movement/jumping, pause, stale-response
takeover, restart, invalid-result handling and scripted manual completion.
It does not use synthetic decisions in the playable game.
The real-provider test also requires `PLATFORMER_TEST_PROVIDER=foundry` or
`ollama` and configuration supplied through `Foundry__*` / `Ollama__*`
environment variables. It makes up to 20 billable calls and stops after
110 seconds; it is skipped unless explicitly enabled. It reads this demo's
user secrets with environment overrides. Add `PLATFORMER_FULL_RUN=1` to instead
require reaching the exit alive within 180 decisions and 290 seconds; this can
make up to 180 billable calls. The game's existing limits still apply.
Never display secret
environment values while recording.

The tested game packages are MonoGame Framework DesktopGL and Content Builder
Task **3.8.5**, ElBruno.AI.Decisions Foundry and Ollama **0.6.3**, and Microsoft
configuration packages **10.0.0**. The tool manifest pins `dotnet-mgcb`
**3.8.5-preview.5**. Restore lock files are checked in. The game opens a
1280-by-720 desktop window and loads one 40-by-20-tile teaching level.

## Controls

| Key | Effect |
| --- | --- |
| Left/Right arrows or A/D | Move left or right in manual mode |
| Space, Up, or W | Jump in manual mode |
| M | Take manual control |
| F | Select Foundry |
| O | Select Ollama |
| P | Pause or resume simulation and new requests |
| N | Restart the same level and reset this run's counters and simulation timer |
| Q or Escape | Cancel pending work and quit |

Manual play is the default and does not construct a provider client or make a
network request. During inference the renderer and keyboard handling continue,
but player physics, enemies, simulation time, and collision updates are paused.
An accepted action controls horizontal movement and the jump button for six
fixed 60 Hz simulation steps, exactly 100 ms. Wall-clock inference time never
becomes physics time. A `jump_*` choice holds jump for all six steps; this
preserves the upstream variable-height jump behavior, including its airborne
hold behavior. A non-jump choice releases the jump button.

The app allows one request at a time. Provider changes, restart, manual takeover,
and quit invalidate the request generation and request cancellation. The app
waits for cancellation to drain before it can issue another request. A timeout,
provider/configuration error, or illegal result stops automatic decisions;
press M or N to recover. No automatic retry, provider fallback, upload, or shared
results file is used. AI runs stop on victory, defeat, or the two-minute
simulation timer, after 300 AI wall-clock seconds, or after 600 requests. Each
provider client also has a 30-second request timeout. Already-received provider
work may still be billed after cancellation. The HUD's session wall clock
includes manual play, while its AI wall clock counts only time spent in an AI
mode and drives the 300-second limit.
The HUD counts user pauses and marks runs that advanced under multiple control
modes as MIXED CONTROLS. Pauses remain in wall time. Do not rank paused or
mixed-control sessions as provider speed comparisons.

## Provider configuration

Use the episode's setup helper before streaming or screen sharing:

```powershell
pwsh -File ..\..\Setup-UserSecrets.ps1 -ValidateOnly
pwsh -File ..\..\Setup-UserSecrets.ps1
```

The helper configures this project's independent user-secrets store with the
Foundry resource root and deployment name. Select the optional Ollama prompt to
also configure `Ollama:Endpoint` and `Ollama:Model`. Foundry can use Azure CLI
identity (`az login` and deployment access required) or a key entered through
hidden input. No cloud resources are provisioned. Manual play needs none of
these settings. The app displays the provider and configured model/deployment
label, but not endpoints or credentials.

For Ollama, the default model label is `nimble:latest` and the default endpoint
is local `http://localhost:11434`. A compatible native decision model and a
running Ollama service are required. A normal chat model is not interchangeable.

Foundry requests may incur service charges and consume quota. Manual play makes
no model calls. Local Ollama uses local compute, memory, disk, and electricity.
The SDK may expose some usage fields and omit others. Missing values are shown
as `unknown` with per-field coverage; reported zero remains zero. The game does
not infer a total from input/output counts or double-add cached/reasoning tokens.
A displayed selected-label probability is only what the SDK reports, not a
calibrated confidence or correctness guarantee.

To remove only this demo's configured keys:

```powershell
dotnet user-secrets remove "Foundry:Endpoint" --project .\Platformer2D.csproj
dotnet user-secrets remove "Foundry:Model" --project .\Platformer2D.csproj
dotnet user-secrets remove "Foundry:ApiKey" --project .\Platformer2D.csproj
dotnet user-secrets remove "Ollama:Endpoint" --project .\Platformer2D.csproj
dotnet user-secrets remove "Ollama:Model" --project .\Platformer2D.csproj
```

These commands affect only this project's user-secrets store. They do not delete
or change any Foundry deployment. User secrets are local development storage,
not encrypted production storage.

## Implementation map

- `Desktop\Program.cs`: small configuration/entry point; no provider is created at startup.
- `Core\DecisionActions.cs`: six options, exact control semantics, label/probability validation, and usage coverage.
- `Core\DecisionProviderFactory.cs`: lazy provider construction and provider-neutral async call.
- `Core\Game\Level.cs`: immutable JSON state built from live player, nearby collision tiles, enemy positions/patrol velocity, gems, exit, and timer.
- `Core\Game\Player.cs`: the model may supply only movement and held-jump input; original physics remain authoritative.
- `Core\Screens\GameplayScreen.cs`: asynchronous single-flight lifecycle, fixed-step actions, pause/restart/takeover, and HUD.
- `Core\Content\Levels\00.txt`: the demo level. No route or winning action is supplied to the model.
- `tests\DecisionContractTests.cs`: action labels, duration, legal probability sets, snapshot context, and nullable usage.

The text observation uses world-pixel coordinates (x increases right, y increases
down; player position is bottom-center; tiles are 40 by 32 pixels). It includes
player position, velocity, grounded/alive state, local collision tiles, nearby
live enemies and their current patrol velocity, uncollected gems, exit delta,
remaining simulation time, and gems remaining. Derived collision geometry is
application observation processing, not model reasoning and not a hidden path.
Grounded observations explicitly list gaps at foot height and distance from the
player's collision edge. `withinJumpApproach` marks gaps within two tile widths
and same-height enemies ahead within three tile widths. These are local geometry
cues, not a route or an automatic jump override. Jump state comes from the actual
physics latch: `jumpHeld`, `canStartJump`, and `canExtendJump`. Instructions tell
the model to jump before losing ground, keep forward momentum/hold during ascent,
and release on descent to rearm. The chosen action is still applied unchanged.

## Upstream attribution and license scope

The game is adapted from **MonoGame/MonoGame.Samples Platformer2D** at commit
[`11b0a41e4ea5eed51b33bf25473db5082769569d`](https://github.com/MonoGame/MonoGame.Samples/commit/11b0a41e4ea5eed51b33bf25473db5082769569d)
on branch `3.8.5`. The [pinned sample README](https://github.com/MonoGame/MonoGame.Samples/blob/11b0a41e4ea5eed51b33bf25473db5082769569d/Platformer2D/README.md)
describes the Starter Kit as including game code and assets. The copied
Core/Desktop source and bundled game content are distributed with the upstream
repository's full [`LICENSE.TXT`](UPSTREAM-LICENSE.TXT),
which contains Ms-PL terms and a separate MIT notice for identified Mono.Xna
portions. The MonoGame README traces the sample to the original XNA Platformer
Starter Kit. No separate asset license or attribution file was found inside the
pinned Platformer2D tree; this demo preserves the complete upstream license and
sample assets together. Review the full notices for the precise scope before
redistributing the demo; this documentation is not legal advice.

The bundled Roboto Bold font has a separate Apache 2.0 license, verified from
its embedded copyright/license metadata. The complete license and Google
copyright attribution are included in [third-party notices](THIRD-PARTY-NOTICES.md).
The desktop build copies these notices and the upstream license into its output.

Selected source includes the Core game, input, screen, and settings code, the
DesktopGL entry point/project, its content build description, and the bundled
content used by the single demo level. Changes include targeting .NET 10,
pinning packages/tools, starting directly in manual gameplay, using only level
00, and adding the decision observation, provider lifecycle, control adapter,
HUD, and tests. Upstream code and assets remain governed by their upstream
notices. This adaptation does not imply MonoGame or Microsoft endorsement or
grant trademark rights.

## Rehearsal status

Locked restore and Release build passed. The graphical launch shows the player,
enemies, gems and ground. A viewport mismatch was corrected: the logical
1280-by-720 scene now matches the window, so the player at y=608 is visible.
Nine offline/desktop checks passed, including gap/enemy approach geometry and
the actual jump latch. The initial provider check passed for both Foundry and
Ollama with 20 accepted decisions each.
The real level was completed by explicitly scripted manual controls in a test,
not by a decision model or physical keyboard automation.

Those initial 20-decision checks stopped before the gap. A subsequent full
Foundry run reproduced late jumping: the model walked off the edge before
requesting a jump, then died. After the observation/instruction correction,
two full Foundry runs each reached the exit alive with 41 accepted decisions,
including jumps over the gap and enemies. No scripted action or safety override
was used. This is observed completion of this teaching level, not guaranteed
success or a benchmark. Ollama has not been reverified with the revised cues.
Rehearse your physical keyboard/window focus and configuration before broadcasting.
