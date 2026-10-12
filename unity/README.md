# Offline Unity V1

Desktop hot-seat prototype. Unity renders a generated 3D board and an immediate-mode control interface. It never decides dice, resource changes, legal moves, scores, card effects, or timers. The existing C# engine runs in a separate local process; there is no HTTP server, database, account, or Internet connection during play.

## Open and play

1. Clone the `feature/unity-ui` branch, or switch to it in your existing checkout.
2. Install .NET SDK 10.0.100 and Unity 6.0 LTS through Unity Hub. The project uses `6000.0.23f1` as its baseline; a newer 6.0 LTS patch can upgrade it. Install the desktop build module for your platform if you want a standalone player.
3. In the repository root, run:

   ```bash
   dotnet restore Game.sln --locked-mode
   dotnet build Game.sln -c Release --no-restore
   ```

4. In Unity Hub, **Add project from disk** and choose `unity/Catan.Client` (not the repository root).
5. Let Unity resolve the pinned packages, then select **Catan → Create prototype scene**. Save this scene when prompted. This prepares the build scene too.
6. Select the `Catan offline prototype` object. Its **Dotnet Executable** defaults to `dotnet`; use a full executable path if Unity Hub does not inherit your PATH. On this cloud machine that path is `/workspace/.tools/dotnet/dotnet`. Leave **Repository Root** blank for the checked-in folder layout, or set it explicitly if you move the Unity project.
7. Press **Play**. Enter 2–4 comma-separated names and a seed, then click **New game**.
8. Select each player and click **Ready**. Select the host (first player) and click **Start**.
9. Click **Switch to active player**, click a cyan legal vertex, then **Place initial settlement**. Click a legal teal edge and **Place initial road**. Repeat for all snake-order placements.
10. During turns, select the active player, **Roll dice**, and follow the displayed phase. Click a hex before **Move robber**, set an eligible victim when needed, and use build/trade/card controls during legal phases. **End turn** advances play.

The timer is real and the backend runs it even while you scroll or wait. Setup choices time out in 30 seconds; roll in 15; main actions in 90. Polling updates authoritative state twice per second. Invalid moves show `Rejected` and the state remains intact. Clicked IDs are shared between action controls: make sure you selected a vertex for a settlement/city, an edge for a road, or a hex for the robber.

### Hands and actions

The selected actor's resources, development cards, and hidden VP are visible in the right-hand panel. Opponents expose only public counts. Selection is manual for hot-seat play; use **Switch to active player** to hand over the terminal. For discards or accepting another player's trade, select that participant instead of the active player.

Resource bundles use **brick,lumber,wool,grain,ore**. `Give` is used for discards, bank/harbor trades, and player offers; `Take` is used for trades and Year of Plenty. Set the trading partner, then **Offer player trade**. Select that partner, enter the displayed offer ID, then **Accept offer**. The backend rechecks balances atomically and supplies maritime ratios.

Road Building uses `Target ID` and `Second road ID`; use -1 for the second only if the engine permits a single road. Monopoly uses the chosen resource. Card play restrictions remain in the engine. Development cards can also be played before rolling when legal.

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

The first package restore and backend publication need Internet access; gameplay is offline. WebGL, mobile, IL2CPP builds, automated in-game opponents, polished artwork, and online multiplayer are outside V1. Automated complete-game simulations remain available in the existing console project.

## V2 boundary

`IGameTransport` is the UI seam. `LocalProcessTransport` launches the desktop backend and exchanges newline-delimited JSON with correlation IDs. UI intent commands have stable request IDs for engine idempotency. The backend sends per-player projections plus legal placement candidates and authoritative clock/deadline values.

For V2, implement an authenticated network transport and server host around `Game.Engine`. Bind the actor/viewer to authenticated identity rather than trusting a supplied name; secure private snapshots; validate protocol versions; manage lobbies, room-code uniqueness, reconnection, durable state, and reconnecting command receipts. The current local adapter trusts the desktop hot-seat user and is deliberately **not** an online security boundary. No gameplay rules need to move into Unity.

## Validation status

The .NET build, 68 automated tests, shared Unity wire DTOs/process transport against the actual host, and published Linux x64 executable are exercised in the cloud environment. The self-contained publication test runs without dotnet on PATH. `scripts/check.sh` also runs the existing 1,000 complete seeded games.

Unity Editor is not installed in the cloud machine. Editor import, shader/scene rendering, mouse picking, Play mode, and standalone Unity builds have not been executed here; verify these with steps 4–10 on your machine. Other operating systems' self-contained publications are documented but untested. The original all-inactive rule proposal remains pending in `docs/DECISIONS.md`.
