# Catan — Stage 1

A terminal-playable, authoritative implementation of classic base-game Catan for 2–4 players. Original source; no Unity, networking, database, or hosting dependencies. [GAME_RULES.md](GAME_RULES.md) is the supplied gameplay specification.

## Unity offline prototype

The `feature/unity-ui` branch adds a Unity 6 LTS desktop hot-seat UI and a local C# engine host. See [Unity setup and play instructions](unity/README.md). The desktop UI uses a local process; a future online version can replace the transport with an authenticated connection.

## Run

Requires the pinned .NET 10 SDK on Linux x64. Cloud setup installs it locally and verifies its archive against Microsoft's SHA-512 release metadata:

```bash
bash scripts/setup.sh
export PATH="/workspace/.tools/dotnet:$PATH"
export DOTNET_CLI_HOME=/workspace/.tools
export NUGET_PACKAGES=/workspace/.tools/nuget
dotnet run --project src/Game.Console --configuration Release --no-build
```

If the SDK is already on your PATH outside the cloud machine, `dotnet restore Game.sln --locked-mode` and `dotnet build Game.sln` are sufficient. `global.json` pins the SDK; NuGet lockfiles pin dependencies. `CATAN_TOOLS_ROOT` overrides the cloud tools location.

For a guided manual check, see [Console testing](docs/CONSOLE_TESTING.md).

At the terminal:

```text
create-game 42
add-player Ryan
add-player John
use Ryan
ready
use John
ready
use Ryan
start-game
show-state
show-board
```

The roll-off determines who places first. `show-state` shows the active player and phase. Switch with `use NAME`, use `legal` to list candidate placements, then `place-settlement VERTEX` and `place-road EDGE`. Repeat the snake setup; resources are awarded from the second settlement. On each normal turn, `roll`, resolve any `discard`, `move-robber`, and `choose-victim` decisions, then trade/build/play cards and `end-turn`. `help` lists every command. Resource bundles are five comma-separated counts in **brick, lumber, wool, grain, ore** order. The selected player's private state is shown only in their own view.

The host runs real timers even while waiting for terminal input: 15 seconds to roll, 30 seconds for mandatory choices, 90 seconds for actions, and 10 seconds for a disconnected player's turn. Setup decisions also receive the mandatory-choice timer. Trading does not extend the action deadline. A Knight before rolling preserves the original roll deadline; overdue rolls occur once its mandatory sequence completes. A Knight during actions preserves the original action deadline; if it expires, outstanding mandatory robber choices are completed automatically before advancing the turn.

## Testing and GitHub Actions

GitHub Actions is manual only on this branch. Once this workflow version is on the default branch, GitHub exposes **Actions → Stage 1 validation → Run workflow**. Until then, use the local commands below. It restores, builds, runs tests, and simulates games; it does not package or deploy the game. Local checks also test first. To additionally check the Linux offline executable after tests pass, use `CATAN_CHECK_PACKAGE=1 bash scripts/check.sh`.

## Verify

```bash
bash scripts/check.sh                 # release build, all tests, 1,000 complete games
bash scripts/check.sh 10 200          # ten games beginning at seed 200
# Once built:
dotnet run --project src/Game.Console -c Release --no-build -- simulate 1000 1
dotnet run --project src/Game.Console -c Release --no-build -- simulate 1 42 4
```

Simulations cycle through 2, 3, and 4 players unless a player count is supplied. Every game must finish with a winner within a bounded turn/command budget, or the command fails with its seed and player count. Replay a failure with `simulate 1 SEED PLAYERS`. Automated players use public engine commands and their own authorized views; they do not inject dice, resources, scores, or privileged state. Invariants run after commands and timeouts, checking resource, development-card, building, and road inventories.

## Projects

- `Game.Contracts`: intent commands and detached per-player snapshots.
- `Game.Engine`: board generation, seeded randomness, lobby, roll-off, placement, production, robber, building, trading, development cards, graph scoring, victory, timers, reconnection, surrender, command receipts, and headless simulation driver.
- `Game.LocalHost`: offline JSON process adapter around the engine; no network listener.
- `Game.Console`: parsing, presentation, actor selection, and the authoritative timer host. It contains no gameplay rules.
- `Game.Engine.Tests`: deterministic gameplay, rejection, boundary, privacy, graph, timer, isolation, concurrency, and complete-game tests.

## Architecture boundaries

Each `GameEngine` instance owns one independent game and serializes mutations under a lock. Commands work on cloned state and cloned PRNG state; rejected commands commit neither. Successful commands and timeout transitions advance the state version. `RequestId` supports retries: identical player/request/intent combinations return the original receipt; changed intent with the same ID is rejected. Receipts are retained for the lifetime of the in-memory game.

The host owns the injected `IClock` and calls `Tick`; `Execute` also processes elapsed deadlines before evaluating a command, so a late command cannot beat a timer. `ManualClock` is available for tests. After a long host pause, each tick processes at most 10,000 elapsed transitions; further ticks finish catching up.

`View(playerId)` is a detached projection, not live mutable state. Only that player's resource identities, card identities, and total including hidden VP cards are returned. Other players receive public counts and visible points. A future network adapter must authenticate the actor, authorize snapshot access, supply request IDs, and own the clock; the local console deliberately allows switching actors for hot-seat play. GUID game IDs are unique independently of gameplay seeds. Room codes are local hints; a future online lobby must check collisions when allocating them.

Game state is in memory. Network persistence, authentication, and online room-code allocation belong to later stages. [docs/DECISIONS.md](docs/DECISIONS.md) records the outstanding all-inactive rule decision; it is not silently added to the authoritative rules.
