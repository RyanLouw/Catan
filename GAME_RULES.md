# GAME_RULES.md

## 1. Purpose

This document is the authoritative gameplay specification for the project.

All gameplay behavior implemented in `Game.Engine` must follow this document.

The target behavior is the classic base-game Catan ruleset, implemented using original source code and original assets.

If implementation encounters a genuine ambiguity not covered by this document or by the referenced classic base-game behavior, do not silently invent a rule. Record the ambiguity and request a decision.

---

# 2. Core Game

## 2.1 Players

The game supports:

- Minimum: 2 players
- Maximum: 4 players

For Version 1, two-player games use the same core gameplay rules as other games.

Special two-player balancing rules are not part of Version 1.

If playtesting later shows that two-player games require balancing changes, those changes must be explicitly specified before implementation.

---

## 2.2 Victory

The victory target is:

**10 Victory Points**

The authoritative Game Engine determines victory.

Once a player legally reaches the victory condition according to the base-game rules, the game ends.

No gameplay command may modify a completed game.

---

## 2.3 Expansions

Version 1 contains no expansion mechanics.

Only classic base-game mechanics are in scope.

---

# 3. Board

## 3.1 Structure

Use the standard classic base-game board structure.

The logical board contains:

- terrain hexes
- desert
- number tokens
- intersections
- edges
- coastal positions
- harbors/ports
- robber position

The Game Engine owns the logical board.

Unity will eventually render the board but will not determine legal board behavior.

---

## 3.2 Resources

The five resources are:

- Brick
- Lumber
- Wool
- Grain
- Ore

The desert produces no resource.

---

## 3.3 Board Randomization

Each new game uses a randomized board.

Randomization includes the appropriate randomized elements of the classic base-game setup.

High-probability number tokens:

- 6
- 8

must not be placed adjacent to another 6 or 8.

Board generation must guarantee that the generated board satisfies all configured generation rules.

---

## 3.4 Deterministic Randomness

All randomness must be injectable or seedable.

This includes:

- board generation
- dice rolls
- development-card shuffle
- random resource stealing
- automatic timeout choices

A known seed must reproduce the same random behavior.

This is mandatory for debugging and automated tests.

---

# 4. Lobby

## 4.1 Creating a Game

A player may create a game.

The creator becomes the:

**Host**

Each game receives a unique:

`GameId`

The online version will also expose a short room code that friends can use to join.

---

## 4.2 Joining

Players may join a game before it begins.

Maximum players:

**4**

Once gameplay begins, additional players cannot join as active players.

---

## 4.3 Ready State

Each player has a Ready state.

The host may start the game when:

- at least 2 players are present
- no more than 4 players are present
- every participating player is Ready

---

## 4.4 Spectators

Spectators are not supported in Version 1.

---

# 5. Starting Player

Before initial placement, all players roll two six-sided dice.

The player with the highest total becomes the starting player.

If multiple players tie for the highest roll, only those tied players reroll.

This continues until one starting player remains.

---

# 6. Initial Placement

Initial placement uses snake order.

Example with four players:

```text
Player 1
Player 2
Player 3
Player 4
Player 4
Player 3
Player 2
Player 1
```

Each player places the appropriate initial settlement and connected road.

Normal placement restrictions apply.

Starting resources are awarded according to classic base-game behavior.

The Game Engine calculates starting resources from authoritative board state.

---

# 7. Turn Structure

A normal connected-player turn operates as follows:

```text
TURN START
    ↓
15-second roll timer
    ↓
Player rolls OR server auto-rolls
    ↓
Resolve dice
    ↓
Resolve resource production
OR
Resolve mandatory roll-7 actions
    ↓
90-second action timer
    ↓
Player may trade/build/buy/play legal actions
    ↓
Player ends turn
OR
90 seconds expires
    ↓
TURN END
    ↓
NEXT PLAYER
```

---

# 8. Dice

The active player rolls two six-sided dice.

The Game Engine/server is authoritative for the result.

The client must never submit an authoritative dice result.

---

# 9. Roll Timer

At the beginning of a connected player's turn, they have:

**15 seconds**

to initiate the dice roll.

If they do not roll within 15 seconds:

**the server automatically rolls for them.**

The dice result is then resolved normally.

---

# 10. Resource Production

When the dice result is not 7, resource production follows classic base-game behavior.

Production depends on:

- rolled number
- terrain
- adjacent buildings
- building type
- robber position
- available supply where applicable

The Game Engine calculates all resource changes.

---

# 11. Rolling 7

Rolling 7 begins the mandatory robber sequence.

Conceptually:

```text
Roll 7
    ↓
Required players discard
    ↓
Active player moves robber
    ↓
Determine eligible victims
    ↓
Select victim where applicable
    ↓
Random eligible resource stolen
    ↓
Normal action phase
```

The engine must model these as explicit states rather than one uncontrolled operation.

---

# 12. Mandatory-Action Timer

Mandatory player decisions have a:

**30-second timer**

Examples include:

- selecting required cards to discard
- selecting robber destination
- selecting an eligible victim

If the player does not respond in time, the authoritative server automatically completes the required action using a valid legal choice.

Automatic choices may use controlled randomness where appropriate.

Timeout randomness must use the same injectable random system used elsewhere in the engine.

A mandatory action may never leave the game permanently blocked.

---

# 13. Main Action Timer

After the dice result and all mandatory effects caused by the roll have been completely resolved, the player receives:

**90 seconds**

for their normal action phase.

The timer does not begin while unresolved mandatory dice effects remain.

---

# 14. Main Actions

During the normal action phase, the active player may perform any actions allowed by the classic base-game rules, including where applicable:

- building roads
- building settlements
- upgrading settlements
- buying development cards
- playing permitted development cards
- trading with players
- trading with the bank
- trading through ports
- ending their turn

Legal ordering and restrictions follow classic base-game behavior.

---

# 15. Trading and Timer

Trading uses the existing 90-second action timer.

Starting a trade:

**does not pause the timer.**

Negotiating a trade:

**does not extend the timer.**

If the active player's action timer expires, unresolved optional trade activity ends and the turn ends.

---

# 16. Action Timer Expiration

When the 90-second action timer reaches zero:

- unresolved optional actions are cancelled
- the active player's turn immediately ends
- the next eligible player's turn begins

The server is authoritative for timer expiration.

The client merely displays the timer.

---

# 17. Building

Building follows classic base-game rules.

The Game Engine validates all requirements.

This includes:

- resource cost
- ownership
- location availability
- connectivity
- settlement distance
- current player
- current phase
- building inventory

A rejected build must not partially change game state.

---

# 18. Roads

Road placement follows classic base-game behavior.

The engine determines legal edges and connectivity.

---

# 19. Settlements

Settlement placement follows classic base-game behavior.

This includes the settlement-distance restriction and required road connectivity during normal gameplay.

---

# 20. Cities

A city may only upgrade an eligible settlement belonging to the same player.

The engine validates the upgrade and cost atomically.

---

# 21. Costs

All standard building and purchase costs follow classic base-game behavior.

Costs must have one authoritative representation in the Game Engine.

Do not duplicate authoritative cost logic across:

- Unity
- server controllers
- SignalR hubs
- database code
- UI code

---

# 22. Player Trading

Player-to-player trading follows classic base-game behavior.

Before committing a trade, the engine must verify that all involved players still possess the resources being exchanged.

Trade execution must be atomic.

Either the complete trade succeeds or nothing changes.

---

# 23. Bank and Harbor Trading

Standard bank and harbor trading behavior applies.

The Game Engine determines the player's legal trading ratios.

The client cannot declare its own trade ratio.

---

# 24. Development Cards

Development-card behavior follows classic base-game rules.

The authoritative engine owns:

- deck creation
- shuffle
- draw
- ownership
- play restrictions
- effects
- scoring

Private development-card information must only be visible to the owning player.

A client must never receive another player's hidden card identities.

---

# 25. Longest Road

Longest Road follows classic base-game behavior.

Implementation must correctly handle:

- branches
- loops
- interruptions
- opponent settlements
- ties
- transfer of ownership

This system requires dedicated graph-based automated tests.

---

# 26. Largest Army

Largest Army follows classic base-game behavior.

The authoritative Game Engine calculates qualification, ownership and victory points.

---

# 27. Victory Points

Victory Points are calculated entirely from authoritative game state.

Clients may display the score but cannot modify or submit an authoritative score.

---

# 28. Disconnects

A network disconnect does not immediately remove a player from the game.

The player becomes:

`Disconnected`

Their existing pieces and game state remain unchanged.

---

# 29. Disconnected Player Turn

When a disconnected player's turn arrives, the server provides a:

**10-second reconnection grace period**

Conceptually:

```text
Disconnected player's turn
        ↓
10-second reconnect grace
     ↙             ↘
Reconnect       Still offline
    ↓                ↓
Resume turn       Skip turn
```

If the player remains disconnected after 10 seconds:

**their entire turn is skipped.**

The game does not wait through the normal 15-second roll timer and 90-second action timer.

---

# 30. Reconnection

A disconnected player may reconnect later.

After authentication and authorization, the player receives the current authoritative game state.

The client reconstructs itself from that state.

The system must never depend on the reconnecting client having received every previous realtime event.

---

# 31. Surrender

A player may intentionally surrender.

After surrendering:

- the player becomes permanently inactive for that game
- future turns are skipped immediately
- no reconnect grace timer applies
- existing roads remain
- existing settlements remain
- existing cities remain
- existing pieces are not removed from the board
- the player cannot win

Removing pieces during surrender is prohibited because doing so could fundamentally alter board connectivity and availability.

Exact interactions between surrendered pieces and rules such as road interruption continue to use the physical board state unless explicitly specified otherwise.

---

# 32. Invalid Commands

Invalid commands must not partially mutate state.

Examples include:

- acting during another player's turn
- rolling twice
- building without resources
- building at an occupied location
- illegal road placement
- illegal settlement placement
- playing an unavailable card
- accepting an invalid trade
- spending the same resources twice
- issuing actions after game completion

A failed command must leave `GameState` valid.

---

# 33. Duplicate Commands

The architecture must eventually protect against duplicated network commands.

Duplicate requests must never cause:

- duplicate dice rolls
- duplicate buildings
- duplicate purchases
- duplicate resource spending
- duplicate trades
- duplicate development-card plays

Stage 1 should design engine operations so that this protection is practical when networking is added.

---

# 34. Game Isolation

Every game has a unique `GameId`.

Game state must never be shared accidentally between games.

Example:

```text
Game A:
Player builds Road 42

Game B:
Road 42 remains completely unchanged
```

Game isolation must eventually have automated integration tests.

---

# 35. Authoritative State

The central architecture principle is:

> The client asks. The server decides.

The authoritative system owns:

- dice
- legal moves
- resources
- cards
- board state
- scores
- turn ownership
- timers
- trades
- robber
- victory
- game completion

Unity is never authoritative.

---

# 36. Hidden Information

Hidden information must only be sent to players authorized to see it.

Do not broadcast secret information and rely on the Unity UI to hide it.

This applies particularly to development cards and any other private player state.

---

# 37. Stage 1 Architecture

Stage 1 builds the gameplay system without networking or graphical UI.

Required projects:

```text
Game.sln

src/
    Game.Engine/
    Game.Contracts/
    Game.Console/

tests/
    Game.Engine.Tests/
```

`Game.Engine` must not depend on:

- Unity
- ASP.NET Core
- SignalR
- Entity Framework Core
- SQL Server
- HTTP
- graphical UI frameworks

---

# 38. Console Runner

The complete game must eventually be playable from a terminal.

Example:

```text
dotnet run

create-game
add-player Ryan
add-player John
start-game

show-board

place-settlement ...
place-road ...

roll

show-state
build-road ...
trade ...
buy-card ...
end-turn

...

winner
```

The console application is only an interface to the Game Engine.

It must not contain gameplay rules.

---

# 39. Testing Requirements

Every gameplay feature requires automated tests.

Tests must include:

- valid behavior
- invalid behavior
- boundary cases
- wrong-player actions
- wrong-phase actions
- insufficient resources
- occupied locations
- illegal connectivity
- deterministic randomness
- state integrity after rejection

A failing operation must not corrupt game state.

---

# 40. Bug-Fixing Rule

When a test fails:

1. Stop adding functionality.
2. Reproduce the failure with the smallest possible test.
3. Determine the root cause.
4. Fix the smallest responsible component.
5. Run the focused test.
6. Run related tests.
7. Run the complete regression suite.
8. Continue only after everything passes.

Never solve a failure by:

- deleting the test
- weakening the assertion
- skipping the test
- swallowing the exception
- hardcoding specifically for the test
- changing expected behavior merely to match broken code

Every discovered gameplay bug should receive a permanent regression test.

---

# 41. Full-Game Simulation

Passing unit tests alone is not sufficient to complete Stage 1.

Automated players must be capable of exercising complete games.

Run progressively larger simulations such as:

```text
1 game
10 games
100 games
1,000+ games
```

Look for:

- crashes
- impossible states
- negative resources
- duplicate pieces
- illegal roads
- illegal settlements
- incorrect scoring
- invalid turn transitions
- state-machine deadlocks
- games that cannot finish
- resource corruption

Random simulation failures must be reproducible using their random seed.

---

# 42. Stage 1 Definition of Done

Stage 1 is complete only when:

- core gameplay is implemented
- the game can be played through the console
- 2–4 players are supported
- deterministic tests exist
- invalid commands preserve state
- complete games can finish
- simulation testing passes
- no Unity dependency exists
- no network dependency exists
- no database dependency exists
- no hosting dependency exists

Only after Stage 1 is stable should development proceed to the Unity client.

---

# 43. Locked Online Timing Rules

| Situation | Rule |
|---|---|
| Connected player's roll | 15 seconds |
| Roll timer expires | Server auto-rolls |
| Mandatory choice | 30 seconds |
| Mandatory timer expires | Server makes legal automatic choice |
| Normal action phase | 90 seconds |
| Trading | Uses same 90-second timer |
| Trading pauses timer | No |
| Action timer expires | End turn |
| Disconnected player's turn | 10-second reconnect grace |
| Still disconnected after grace | Skip entire turn |
| Surrendered player's turn | Skip immediately |

---

# 44. Locked Project Decisions

| Item | Decision |
|---|---|
| Players | 2–4 |
| Victory | 10 VP |
| Ruleset | Classic base game |
| Expansions | None in V1 |
| Two-player special rules | None in V1 |
| Board | Standard base-game structure |
| Random board | Yes |
| Adjacent 6/8 | Prohibited |
| Starting player | Two-dice roll-off |
| Starting-player ties | Reroll between tied players |
| Trading | Included |
| Robber | Included |
| Development cards | Included |
| Longest Road | Included |
| Largest Army | Included |
| Spectators | No in V1 |
| Lobby | Host + Ready system |
| Room codes | Yes |
| Reconnection | Supported |
| Surrender | Supported |
| AI replacement | No in V1 |
| Stage 1 UI | Console |
| Stage 1 Unity | None |
| Stage 1 networking | None |
| Stage 1 database | None |
| Authoritative rules | `Game.Engine` |