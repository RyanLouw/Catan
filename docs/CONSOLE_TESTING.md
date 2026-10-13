# Test from the console

In the cloud terminal:

```bash
cd /workspace/Catan
export PATH="/workspace/.tools/dotnet:$PATH"
export DOTNET_CLI_HOME=/workspace/.tools
export NUGET_PACKAGES=/workspace/.tools/nuget
dotnet run --project src/Game.Console -c Release --no-build
```

For a fresh clone on your own machine, install .NET SDK 10.0.100, open the repository directory, then run `dotnet restore Game.sln --locked-mode`, `dotnet build Game.sln -c Release`, and the `dotnet run` command above. The cloud-specific exports are unnecessary when .NET is installed normally.

At the game's `>` prompt, enter one command at a time:

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

Expect 19 hexes, 72 edges, a phase, and an `ActivePlayer`.

For initial placement:

1. Select the active player with `use Ryan` or `use John`.
2. Enter `legal` and choose an available settlement ID.
3. Enter `place-settlement ID`, replacing `ID` with the actual number.
4. Enter `legal` again and choose an available road ID.
5. Enter `place-road ID`.
6. Enter `show-state` and repeat for the next active player.

Each player places two settlements and two roads in snake order. Timers run while you type, so the engine may automatically place a piece or roll. Check `show-state` before acting.

During normal turns, use:

```text
roll
show-state
legal
build-road ID
build-settlement ID
build-city ID
buy-card
end-turn
winner
help
```

Builds require resources; these are examples of available commands, not a sequence guaranteed to succeed. Illegal actions print `Rejected` and leave resources and pieces intact. On a 7, follow the phase using `discard`, `move-robber`, and `choose-victim`; `help` shows syntax. Resource bundles use brick,lumber,wool,grain,ore counts, for example `discard 1,0,1,0,0` when exactly those two cards must be discarded.

Type `quit` to return to the shell. For an automatic check:

```bash
dotnet test Game.sln -c Release --no-build
dotnet run --project src/Game.Console -c Release --no-build -- simulate 10 1
```

Expect 103 passing tests and 10/10 completed games with winners. For the full validation run, use `bash scripts/check.sh` in the cloud environment.
