# Decision Space Game

Launch with **`dotnet run`**, no arguments. The game starts in **manual mode**,
without creating a model client or requiring Foundry settings. Press **F** for
Foundry, **O** for Ollama, or **M** to take back control.

The native model chooses `move_left`, `move_right`, `fire`, or `stop`, not generated
JSON or an explanation. Application code owns movement, collision, cooldown,
validation, and score. This finale has more plumbing than the console Hello
examples, with a short entry point and separate rules/driver/renderer.

For two unattended side-by-side missions, optional **`--foundry`** and
**`--ollama`** start immediately and print a **Spectre.Console mission report**
on completion. No other gameplay arguments are needed.

## Setup

- .NET SDK **10.0.401**, patch roll-forward, targeting .NET 10.
- Community SDKs **ElBruno.AI.Decisions.Foundry 0.6.3** and
  **ElBruno.AI.Decisions.Ollama 0.6.3**, not official Microsoft SDKs.
- Configuration packages **10.0.0**, dependency lock files included.
- **Spectre.Console 0.57.2** for styled panels, per-sprite colors, live display,
  and final report tables. No new model/provider or UI framework is introduced.
- A real terminal, minimum **72 columns x 30 rows**; **110 x 32** or larger
  shows a side-by-side telemetry panel. **138 columns** enables wider sprites.
  Windows Terminal or the VS Code terminal works well. ASCII battlefield glyphs
  avoid font/emoji width surprises; Spectre adapts borders/styles to the terminal.
- Manual play needs no credentials, Azure, or Ollama.
- F needs an existing Microsoft-Decision-1 deployment and authorized Azure CLI
  identity, or an API key supplied externally.
- O needs a running Ollama server with native SystemOne support and the installed
  **nimble:latest** decision model. A general chat model is not a substitute.
  Local rehearsal used Ollama **0.40.4**, nimble 9B Q8_0. Nimble is not the
  Microsoft-Decision-1 weights.

From this directory:

```powershell
dotnet restore DecisionSpaceGame.slnx --locked-mode
dotnet run
```

For AI configuration, run off stream:

```powershell
pwsh -File ..\..\Setup-UserSecrets.ps1
```

The helper configures Foundry for the cloud samples. Its optional Ollama prompt
now configures **both demo 02 and this game**. User-secrets ID is unchanged:
`netfoundry-episode01-decision-space-game`. Keys: `Foundry:Endpoint` (resource root),
`Foundry:Model` (deployment), optional `Foundry:ApiKey`, `Ollama:Endpoint`,
`Ollama:Model`. Environment variables override user secrets.

Ollama alone also works with defaults **http://localhost:11434** and
**nimble:latest**. There is no installation or automatic model download.
Foundry is initialized only after pressing F, so missing cloud configuration
does not prevent manual or local play. Missing configuration/provider failures
are visible, not replaced with synthetic actions.

## Keyboard and screen

| Key | Action |
| --- | --- |
| M | Manual control, cancel pending AI work |
| F | Foundry decisions, or explicitly retry after a failure |
| O | Ollama decisions, or explicitly retry after a failure |
| A/Left, D/Right | Move left/right in manual mode |
| Space | Fire in manual mode |
| S | Hold position |
| P | Pause/resume simulation and new AI calls |
| N | New seed-42 mission, reset mission statistics and return to manual control |
| Q/Escape or Ctrl+C | Cancel and quit |

Spectre's live screen shows framed battlefield and flight-telemetry panels.
Invaders use three persistent, animated ASCII designs: scout, fighter and heavy
silhouettes. These are cosmetic designs, not different health or hitboxes:
all 14 enemies remain single-cell, single-hit targets. Wide terminals show
more detailed silhouettes, not larger collision areas. Each shot/enemy/ship has its own color
rather than coloring an entire row. The cyan two-row battleship, magenta/yellow
formation, green laser shots, red hostile shots and dim starfield are distinct.
HUD shows mode/deployment, score, hull, enemies, thinking/error status, last
action, latency, probability/confidence, cumulative usage, shots/hits and timing.
Large terminals use three-character sprites; compact ones use two. Color is supplementary:
glyphs, labels and error text still distinguish objects and state. Terminal
live-display cursor handling is restored on exit, including cancellation.

The HUD explicitly shows **FIRE READY** or the remaining cooldown ticks and
the last **applied** action, not merely a returned suggestion. Ship damage
briefly turns the upper ship red and displays **HIT!**, for six simulation
ticks. Color is not the only cue.

**P** freezes movement and action application, blocks gameplay/provider-switch
keys, and stops new inference requests. An already-running call can finish
and still incur charges; its result waits until resume. P resumes without
catch-up ticks; N/Q remain available while paused. Wall time and the automatic
five-minute safety limit continue. The report counts user pauses so a paused
run is not mistaken for a comparable provider timing.
The final report highlights outcome, score, wall time and reported input/output
tokens before the detailed statistics. No extra waves or difficulty changes
are introduced by these presentation improvements.

In no-argument interactive play, the scene remains after victory/defeat so N can
restart; Q prints the report. There is no manual-session time limit. Optional
auto-start missions are bounded and exit automatically. Mode switches no longer
erase mission totals: the report marks them **mixed-mode**, unsuitable for ranking
providers. N starts fresh statistics. Old in-flight work stays attributed to
its original mission, even if cancellation completes after a restart.
Neither high confidence nor a correct label guarantees a good move.

## Start two consoles and compare the reports

Restore/build **once**, before starting either process:

```powershell
dotnet restore DecisionSpaceGame.slnx --locked-mode
dotnet build -c Release --no-restore
```

Then open two terminals in **this directory**:

```powershell
# Terminal 1
dotnet run --no-build -c Release -- --foundry

# Terminal 2
dotnet run --no-build -c Release -- --ollama
```

The `--` belongs to `dotnet run`: it forwards the selected switch to the app.
The compiled executable also accepts `--foundry` or `--ollama` directly.
No arguments still works as before. Both flags together or unknown arguments
print usage and exit 2 before any inference; `--help` prints usage.
Avoid rebuilding/restoring while either executable is running on Windows.

Both missions use **seed 42**, identical initial state, legal-option filtering,
four-tick AI actions, and **the same stop limits**: victory, defeat, provider
failure, **1,200 simulated ticks (60 simulated seconds)**, or **300 wall seconds**,
whichever comes first. Q/Ctrl+C also stop the mission. The limits prevent runaway
cloud calls during unattended rehearsals. A failure/cancelled call may still bill.
No automatic fallback, retry loop, synchronized cross-process start, shared file,
or results upload is involved.

The final table includes:

| Statistic | How to read it |
| --- | --- |
| Outcome / provider / actual deployment / seed | Rank outcomes before times. Victory, defeat, limits, quit, failure are distinct. |
| Wall elapsed / simulated seconds and ticks | Wall includes waiting/authentication/warm-up, excludes final drain/reporting. AI simulation time pauses during inference. |
| Score, kills, enemies left, remaining hull | Faster defeat is not faster success. |
| Shots fired and hit rate | Kills divided by shots fired, including shots still airborne at stop. |
| Requests issued/completed, applied/rejected/failed/cancelled | Includes mode-switch/quit cleanup; requests are logical SDK calls, not a billing ledger. |
| Mean / p50 / p95 decision latency, sum of call durations | Nearest-rank percentiles over completed requests, including failures/cancellation; no fabricated zero when no calls complete. |
| Reported input/output/total/reasoning/cached tokens and coverage | Nullable fields aggregated independently; missing remains unknown, no inferred totals, no double-addition of reasoning/cached tokens. |
| Applied action mix | Counts of actually applied labels, not every returned suggestion. |

Tokens include all returned decision responses, even if discarded or cancelled
after a response arrived. Errors without a response have no usage, not zero.
Each field shows coverage against returned decision responses, and request counts
show calls with no such response. Charges may therefore exceed the displayed usage.

**"Which ended faster" means wall elapsed for comparable outcomes**, preferably
both victories without manual intervention. Different model choices can yield
different trajectories. These concurrent processes share machine/network resources;
local GPU load, service routing and warm-up differ. It is a visual teaching
comparison, not a controlled provider benchmark or price/savings claim. Compare
multiple runs if evaluating quality; this demo does not automate a winner.

## Why the AI pacing and prompt changed

Previously the simulation advanced during inference while a movement lasted
only **200 ms**. A slow model could decide from a scene seconds out of date;
the ship then spent most of its time idle. The previous five-second freshness
window was also much larger than the action window.

**AI is now explicitly decision-paced:** freeze the world while one request
runs, apply its legal choice, animate **four 50 ms ticks**, then capture a fresh
snapshot. No accumulated wall-clock ticks are replayed when a call returns.
Keyboard/rendering remain responsive. **Manual mode stays real-time.**
This intentionally changes game difficulty: it teaches state-to-action decisions,
not real-time reflexes, network-latency equivalence, or provider speed benchmarks.

`Game.Snapshot()` sends coordinates, cooldown, current bullets, enemy direction
and movement timing, plus **predictedFireLanes**. These deterministic geometric
hints project where an upward shot could meet the moving formation. They use
the same bounce/movement order as the engine, not a hidden model or automatic
action policy. Existing shots can remove targets, so predictions are not promises.
The model still chooses the action and can choose poorly.
The application offers **only currently legal labels**: no fire during cooldown,
no movement beyond a boundary. Its returned distribution is checked against
that exact offered set, not an assumed always-four-label response. This avoids
repeated illegal fire decisions stalling a paused world.

`Actions.Instructions` now agrees with that clock contract and explicitly says
movement spans **four columns**, not one. Priorities are dodge immediate threats,
fire when a predicted lane matches and cooldown is zero, otherwise move toward
a firing lane or wait for cooldown. Both providers receive the same snapshot,
instructions and currently legal option descriptions via native `ChooseAsync`.

Only one request is in flight, including during provider switches. Cancellation
invalidates old-generation results; they cannot move the ship after M/F/O/N.
Current legal-action checks still apply. Invalid results pause AI with an error
instead of automatically repeating requests indefinitely. A 60-second deadline
allows local cold starts; failures stay paused until an explicit key choice.
There is no automatic fallback or offline replay mode in the playable app.

## Reading and checking the code

1. `Program.cs`: external configuration and lazy Foundry/Ollama factories.
2. `Game.cs`: allowed actions, prompt, deterministic rules, tactical snapshot.
3. `DecisionDriver.cs`: one `ChooseAsync`, cancellation and action validation.
4. `GameConsole.cs`: M/F/O selection, decision-paced clock, colored frame.
5. `GamePresentation.cs`: Spectre battlefield, telemetry panel and final table.
6. `MissionStatistics.cs`: observed counters, coverage, latency and action mix.

```powershell
dotnet test DecisionSpaceGame.slnx -c Release
```

Tests cover deterministic rules, predicted shot lanes against actual collisions,
paused-AI/manual clocks, mode cancellation, stale results, provider contracts and
Spectre-rendered frames at two sizes, launch switches, usage coverage, percentiles,
mixed modes, cancellation accounting and fresh-mission reset. Synthetic replay lives only in tests.
Model play quality is not deterministic; a successful inference is not proof of
optimal strategy.

A bounded live rehearsal with the revised prompt and legal-option filtering
completed **20 accepted decisions per provider with no rejected decisions**.
Both Foundry and local nimble scored 30 points in those runs. These are smoke
observations, not expected scores, provider rankings, or a benchmark.

## Costs and cleanup

Each F decision can incur Azure charges and consume quota. O uses local compute,
memory and electricity. AI pauses between turns, but it continues requesting
decisions until M, game over, a provider failure, or quit. Do not leave it running
unattended. Cancellation does not guarantee an already-received request is free.
The telemetry panel/report include input/output counters from SDK 0.6.3.
`not reported` means the provider omitted that counter, not zero or a cost estimate.

Q stops play, cancels outstanding work, and releases both clients. The app creates
no cloud resources. Never delete shared deployments. To remove only game settings:

```powershell
dotnet user-secrets clear --project .\DecisionSpaceGame.csproj
```

Remove `Foundry__*`/`Ollama__*` environment overrides separately. Do not display
configuration or identity diagnostics on stream.

## Resources

- [Native decision API](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision)
- [Community SDK](https://github.com/elbruno/ElBruno.AI.Decisions)
- [Episode](../../README.md)
