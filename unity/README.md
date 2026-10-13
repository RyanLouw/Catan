# Unity desktop UI — V2

Desktop hot-seat prototype. Unity renders a generated 3D board and an immediate-mode control interface. It never decides dice, resource changes, legal moves, scores, card effects, or timers. The existing C# engine runs in a separate local process; there is no HTTP server, database, account, or Internet connection during play.

## Open and play

1. Clone the `feature/unity-ui` branch, or switch to it in your existing checkout.
2. Install .NET SDK 10.0.100 or a later .NET 10 SDK, and Unity 6.0 LTS through Unity Hub. The project uses `6000.0.23f1` as its baseline; a newer 6.0 LTS patch can upgrade it. Install the desktop build module for your platform if you want a standalone player.
3. In the repository root, run:

   ```bash
   dotnet restore Game.sln --locked-mode
   dotnet build Game.sln -c Release --no-restore
   ```

4. In Unity Hub, **Add project from disk** and choose `unity/Catan.Client` (not the repository root).
5. Let Unity resolve the pinned packages, then select **Catan → Create prototype scene**. Save this scene when prompted. This prepares the build scene too.
6. Select the `Catan offline prototype` object. Its **Dotnet Executable** defaults to `dotnet`; use a full executable path if Unity Hub does not inherit your PATH. On this cloud machine that path is `/workspace/.tools/dotnet/dotnet`. Leave **Repository Root** blank for the checked-in folder layout, or set it explicitly if you move the Unity project.
7. Press **Play**. Choose 2–4 players, enter their names, then click **Start game**. The local players are readied and the host starts through engine commands.
8. When the active player changes, use **Continue as NAME** to pass the table to that player. Each seat has its own colour: red, blue, gold, or purple. Their roads, houses, cities, player card, and drag preview use that colour.
9. Drag the **Settlement** icon from the bottom tray onto a glowing intersection. Then drag the **Road** icon onto a glowing edge beside that settlement. Repeat through the snake setup. No IDs need to be typed. The header shows separate setup progress for all settlements and roads; normal roll/build turns begin only after everyone has placed two of each. Each initial road must touch the settlement just placed, including on the second placement pass. Normal roads may connect to any of your settlements or unblocked roads.
10. Use **Roll dice** when the roll phase begins. A short dice animation settles on the two actual backend values. A result panel shows the total and the resource icons/quantities you received, or explicitly says that none were produced. The starting-player roll-off is also displayed at setup. Your hand header retains a text receipt (or “no resources”) for the current roll even after the animation closes. Resource cards retain their `+N` production labels until the next turn, and each player panel shows their public production count. A 7 instead prompts discards and robber movement. Drag new pieces during the action phase, use **Trade** for a resource dialog, then **End turn**. Cities are dragged onto your eligible existing houses. Dropping at an invalid location returns the piece to the tray and never spends resources.

The round countdown uses the authoritative backend deadline. **Relaxed testing pace** is enabled by default when creating a Unity game: **30 seconds between players, 60 seconds to roll or make a mandatory choice (including placement), and 180 seconds for actions**. Click **I’m ready — start now** to end the handover early. Rolling/placement time starts only after the handover ends. Uncheck the setting for the original 15-second roll, 30-second choices, and 90-second actions with no handover. The console still uses the original timings. Trade dialogs do not pause or extend the active action timer. Backend timeouts can advance the phase while you drag; the backend validates the final command again.

### Board and controls

The board is the main play surface. Roads are coloured strips, settlements are roofed houses, and cities have a tower. The bottom tray shows piece icons, available pieces, and resource cost icons supplied by the engine. Your resource hand and score are visible below the board; each opponent's panel displays public counts only. Select a player's panel for hot-seat decisions such as a discard or accepting a trade. Mouse gestures use Unity GUI events rather than the legacy `Input` API, supporting projects with different input settings.

The **Trade** dialog has resource icons and plus/minus selectors instead of comma-separated bundles. Choose a partner, select what to give/receive, then offer the exchange. Select the recipient's panel, open the trade offer, and accept it. Bank/harbor ratios are supplied by the backend. Trade offers are cancelled by the engine when a turn expires.

Rolling 7 opens a discard picker for each affected player. After discards, drag the **Robber** icon onto a different hex, then choose a victim from the prompted player buttons if necessary.

Use **Development cards** to play a held card. Knight prompts robber movement; Year of Plenty and Monopoly have resource pickers. Road Building lets you stage the first free road, highlights the legal second positions through a read-only engine preview, then commits both atomically after the second drop. If no second road is possible, the engine accepts the permitted single road. Cancel before committing to retain the card. Victory-point cards remain passive. New-card and one-card-per-turn restrictions are enforced by the engine.

The corner label reads **UI build 0.8**. An outdated host is rejected with a rebuild instruction instead of silently omitting new dice data. If startup fails, the error appears in the new-game panel and Unity Console. Rebuild Release before Play mode; the Editor always starts that build rather than a potentially stale published executable. The launcher automatically checks the standard Windows .NET installation when Unity Hub lacks a `dotnet` PATH entry. You can still set **Dotnet Executable** and **Repository Root** in the Inspector.

## Standalone desktop packaging

Unity cannot load the .NET 10 engine DLL as a Unity script assembly. The backend must be built as a separate self-contained executable.

From the repository root, on a machine with .NET SDK installed:

```bash
dotnet publish src/Game.LocalHost -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:NuGetLockFilePath=packages.publish.lock.json -o unity/Catan.Client/Assets/StreamingAssets/Backend/win-x64
```

Replace `win-x64` in both places with `linux-x64`, `osx-x64`, or `osx-arm64` for your desktop. A shell helper is also provided:

```bash
bash scripts/publish-unity-backend.sh linux-x64
python3 scripts/test-offline-host.py
```

Then build the saved prototype scene using Unity's desktop build profile with the **Mono** scripting backend. The application finds the matching RID executable in StreamingAssets. No .NET SDK is required on the player's machine. Published binaries and RID-specific publication lockfiles are generated outputs and are excluded from Git. Publication uses separate lockfiles to preserve normal locked restores. Do not copy .NET 10 DLLs into Unity's script/plugin folders. Linux/macOS packaging must preserve executable permissions; use `chmod +x` on the packaged `Game.LocalHost` if the platform's copy/build step drops them. macOS signing/notarization and distributable installers are outside this prototype.

GitHub Actions is manual validation only; there is no automatic deployment. Test in Unity Play mode before creating a standalone package.

The first package restore and backend publication need Internet access; gameplay is offline. WebGL, mobile, IL2CPP builds, automated in-game opponents, polished artwork, and online multiplayer are outside this desktop prototype. Automated complete-game simulations remain available in the existing console project.

## Future online boundary

`IGameTransport` is the UI seam. `LocalProcessTransport` launches the desktop backend and exchanges newline-delimited JSON with correlation IDs. UI intent commands have stable request IDs for engine idempotency. The backend sends per-player projections plus legal placement candidates and authoritative clock/deadline values.

For future online multiplayer, implement an authenticated network transport and server host around `Game.Engine`. Bind the actor/viewer to authenticated identity rather than trusting a supplied name; secure private snapshots; validate protocol versions; manage lobbies, room-code uniqueness, reconnection, durable state, and reconnecting command receipts. The current local adapter trusts the desktop hot-seat user and is deliberately **not** an online security boundary. No gameplay rules need to move into Unity.

## Validation status

The .NET build, 112 automated tests, shared Unity wire DTOs/process transport against the actual host, and published Linux x64 executable are exercised in the cloud environment. The self-contained publication test runs without dotnet on PATH. `scripts/check.sh` also runs the existing 1,000 complete seeded games.

Unity Editor is not installed in the cloud machine. Editor import, shader/scene rendering, mouse picking, Play mode, and standalone Unity builds have not been executed here; verify these with steps 4–10 on your machine. Other operating systems' self-contained publications are documented but untested. The original all-inactive rule proposal remains pending in `docs/DECISIONS.md`.

### Classic settlement distance

Every settlement must leave an empty intersection between itself and every other settlement/city, including friendly buildings. The user confirmed keeping this classic rule. Normal settlements also require an owned connecting road; initial placements do not.

### Production and trading

Every roll except seven produces for all eligible players, regardless of who rolled: a settlement receives one resource per adjacent matching hex and a city receives two. The robber blocks its hex; limited bank supply can prevent production. The dice receipt shows your own resources received and public totals for other players.

During your action phase, open **Trade**, select the quantities to give and receive, choose **Anyone** or a named player, and submit the offer. Switch to another player's hand to inspect and accept it. Acceptance checks both players' current resources and completes the exchange once; outstanding offers end with the turn. The creator can cancel an open offer.

For **Bank / port trade**, give a single resource type and receive a different type. The displayed ratio is 4:1 normally, 3:1 with your settlement/city on a generic port, or 2:1 for the resource matching an owned specific port. The bank must have the requested cards.

### Inspecting resources and choosing dice

Start a **new game** with **Testing: show all resources and allow chosen dice** checked (enabled by default). Each seat displays icons and counts in Brick, Lumber, Wool, Grain, Ore order. The selected player's private cards remain in their own hand. Disable Testing when creating a game for normal hidden resource types and random dice.

After both initial settlements and roads are placed, select the active player and continue through the handover. Check **Choose test dice** below the Roll button. Click **Die 1** and **Die 2** to cycle each face from 1 to 6, then press **Roll dice**. Compare all seat counts with adjacent matching hexes; settlements gain one, cities two, and the robber blocks production. Seven still triggers discards and the robber. Uncheck the control for a random roll; timer-triggered rolls also remain random. Testing mode applies for the whole game and must be selected again for a new game.

### Board and icon presentation

UI build 0.8 adds outlined, shaded icons for resources, roads, settlements, cities, dice, the robber and trade. The board uses terrain colours and small trees, sheep, field rows, clay bricks and ore rocks. Number tokens show probability dots; six and eight use red text. Ports have paired wooden piers, a resource/trade icon, a 2:1 or 3:1 badge and a player-colour ownership marker. The camera fits the surrounding coast and ports. Board materials use simple directional shading without requiring scene lights. These runtime visuals require Unity Play mode review on the user's machine.

### Visible resource activity

The **Activity** panel opens automatically when starting resources or dice production are awarded. It displays each player's resource icons and quantities, including opponents, and explicitly reports rolls that give a player nothing. Scroll to review previous receipts or collapse the panel to see more of the board. Switching the selected hand does not clear the history. The engine retains the latest 120 receipts and the panel displays the latest 40.

Classic setup awards one resource from each adjacent non-desert hex when the **second settlement** is placed, regardless of the hex number. The feed labels this **Second settlement / Starting resources added to hand** and keeps it visible after initial roads are finished. First settlements award no starting cards. Ordinary dice production still respects matching numbers, cities, the robber and bank shortages; seven produces no cards. The public receipts show newly produced cards while other players' full hands and development cards remain private outside Testing mode. Rebuild the backend after pulling this update; the UI requires protocol version 6.

### Building costs and reviewing player trades

Building icons stay visible in player colours. Required costs appear below each piece, and **Need …** lists the exact cards missing from the selected hand. Paid placement cannot start without those resources; the backend rechecks the payment on submission. Initial placement and Road Building card roads remain free. Development-card purchases also show missing resources.

Player trading is available during the active player's Action phase, after rolling. Select give/receive quantities and choose a partner or Anyone. Submit **Offer to …**; the dialog stays open. Use **Review as** to switch to the recipient's hand inside the dialog without ending the offering player's turn, then click **Accept**. Acceptance lists any missing cards and remains disabled until the recipient has them. Switch back with Review as to continue the active player's turn. Offers expire when that turn ends; the timer continues during review. Bank/port trading uses the separate Bank / port trade button.
