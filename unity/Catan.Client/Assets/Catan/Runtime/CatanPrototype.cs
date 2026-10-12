using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Catan.Client
{
    public sealed class CatanPrototype : MonoBehaviour
    {
        public string DotnetExecutable = "dotnet";
        public string RepositoryRoot = "";
        private IGameTransport transport;
        private BoardRenderer board;
        private HostResponse response;
        private string actor = "", names = "Ryan,John", seed = "42", status = "Create a game to begin.", target = "0", secondTarget = "-1", partner = "", offerId = "1";
        private string give = "0,0,0,0,0", take = "0,0,0,0,0", resource = "Brick";
        private readonly string[] resources = { "Brick", "Lumber", "Wool", "Grain", "Ore" };
        private bool busy, destroyed;
        private float nextPoll, receivedAt;
        private long drawnVersion = -1;
        private string drawnActor;
        private Vector2 leftScroll, rightScroll;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CatanPrototype>() == null) new GameObject("Catan offline prototype").AddComponent<CatanPrototype>();
        }
        private void Start()
        {
            board = gameObject.AddComponent<BoardRenderer>();
            try
            {
                string rid = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer ? "win-x64"
                    : Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer ? (SystemInfo.processorType.Contains("Apple") ? "osx-arm64" : "osx-x64") : "linux-x64";
                string executableName = rid.StartsWith("win") ? "Game.LocalHost.exe" : "Game.LocalHost";
                string packaged = Path.Combine(Application.streamingAssetsPath, "Backend", rid, executableName);
                if (File.Exists(packaged)) transport = new LocalProcessTransport(packaged, "", Path.GetDirectoryName(packaged));
                else
                {
                    if (!Application.isEditor) throw new FileNotFoundException("Publish the offline backend into StreamingAssets before building the desktop player.", packaged);
                    string root = string.IsNullOrWhiteSpace(RepositoryRoot) ? Path.GetFullPath(Path.Combine(Application.dataPath, "../../..")) : RepositoryRoot;
                    string dll = Path.Combine(root, "src", "Game.LocalHost", "bin", "Release", "net10.0", "Game.LocalHost.dll");
                    if (!File.Exists(dll)) throw new FileNotFoundException("Build Game.sln in Release first; see unity/README.md.", dll);
                    transport = new LocalProcessTransport(DotnetExecutable, "\"" + dll + "\"", root);
                }
            }
            catch (Exception e) { status = "Backend startup failed: " + e.Message; Debug.LogError(status); }
        }
        private void Update()
        {
            if (!busy && response != null && Time.unscaledTime >= nextPoll) { nextPoll = Time.unscaledTime + .5f; _ = Refresh(); }
            if (board != null && board.BoardCamera != null && Input.GetMouseButtonDown(0) && board.BoardCamera.pixelRect.Contains(Input.mousePosition))
            {
                if (Physics.Raycast(board.BoardCamera.ScreenPointToRay(Input.mousePosition), out var hit))
                {
                    var selected = hit.collider.GetComponent<BoardTarget>();
                    if (selected != null) { target = selected.Id.ToString(); status = "Selected " + selected.Kind + " " + target + ". Choose the matching action."; }
                }
            }
        }
        private async Task<bool> Send(HostRequest request, bool showStatus = true)
        {
            if (transport == null) { status = "Backend unavailable. See startup error and unity/README.md."; return false; }
            var incoming = await transport.RequestAsync(request);
            if (destroyed) return false;
            if (incoming.View != null)
            {
                actor = request.Viewer; response = incoming; receivedAt = Time.unscaledTime;
                if (incoming.View.Version != drawnVersion || actor != drawnActor || incoming.View.GameId != lastGame)
                { board.Render(incoming); drawnVersion = incoming.View.Version; drawnActor = actor; lastGame = incoming.View.GameId; }
            }
            if (showStatus || !incoming.Success) status = incoming.Success ? "Accepted: " + incoming.Message : "Rejected: " + incoming.Message;
            return incoming.Success;
        }
        private string lastGame;
        private async Task Refresh()
        {
            busy = true;
            try { await Send(new HostRequest { Op = "view", Viewer = actor }, false); }
            catch (Exception e) { status = e.Message; nextPoll = Time.unscaledTime + 5; }
            finally { busy = false; }
        }
        private async void NewGame()
        {
            if (busy) return; busy = true;
            try
            {
                var players = names.Split(',').Select(n => n.Trim()).ToArray();
                bool success = await Send(new HostRequest { Op = "create", Viewer = players[0], Seed = ulong.Parse(seed), Players = players });
                if (success) partner = response.View.Players.First(p => p.Id != actor).Id;
            }
            catch (Exception e) { status = e.Message; }
            finally { busy = false; }
        }
        private async void SelectActor(string name)
        {
            if (busy) return; busy = true;
            try { await Send(new HostRequest { Op = "view", Viewer = name }); }
            catch (Exception e) { status = e.Message; }
            finally { busy = false; }
        }
        private async void Command(string kind, bool bundles = false, bool useResource = false)
        {
            if (busy) return; busy = true;
            try
            {
                var intent = new Intent { Kind = kind, Player = actor, Target = int.Parse(target), SecondTarget = int.Parse(secondTarget), RequestId = Guid.NewGuid().ToString("N"),
                    OtherPlayer = kind == "ChooseVictim" || kind == "OfferTrade" ? partner : null, Resource = useResource ? resource : null };
                if (kind == "Ready") intent.Target = 1;
                if (kind == "AcceptTrade" || kind == "CancelTrade") intent.Target = int.Parse(offerId);
                if (bundles) { intent.Give = ParseBundle(give); intent.Take = ParseBundle(take); }
                await Send(new HostRequest { Op = "command", Viewer = actor, Command = intent });
            }
            catch (Exception e) { status = "Input/connection error: " + e.Message; }
            finally { busy = false; }
        }
        private static int[] ParseBundle(string value)
        {
            var values = value.Split(',').Select(int.Parse).ToArray(); if (values.Length != 5) throw new FormatException("Use five counts: brick,lumber,wool,grain,ore."); return values;
        }
        private void OnGUI()
        {
            var originalMatrix = GUI.matrix; GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1600f, Screen.height / 1000f, 1));
            // IMGUI is an overlay: leave the board camera's center viewport unobscured.
            GUI.color = new Color(.045f,.065f,.09f);
            GUI.DrawTexture(new Rect(0,0,440,1000), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(1280,0,320,1000), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(440,0,840,140), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(440,860,840,140), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.skin.label.fontSize = 17; GUI.skin.button.fontSize = 17; GUI.skin.textField.fontSize = 17;
            GUI.Label(new Rect(25,15,1550,45), "CATAN / OFFLINE V1 — local C# engine, Unity presentation");
            GUI.Label(new Rect(455,80,805,55), response == null ? "Create a seeded hot-seat game" : "Turn " + response.View.Turn + "   " + response.View.Phase + "   Active: " + response.View.ActivePlayer + "   Dice: " + (response.View.LastRoll.HasValue ? response.View.LastRoll.ToString() : "—"));
            GUI.Label(new Rect(455,875,800,100), status);
            GUILayout.BeginArea(new Rect(20,70,400,900), GUI.skin.box); leftScroll = GUILayout.BeginScrollView(leftScroll);
            GUI.enabled = !busy;
            GUILayout.Label("Players (2–4, comma separated)"); names = GUILayout.TextField(names); GUILayout.Label("Random seed"); seed = GUILayout.TextField(seed);
            if (GUILayout.Button("New game")) NewGame();
            if (response != null)
            {
                GUILayout.Space(12); GUILayout.Label("Selected actor / private hand");
                foreach (var p in response.View.Players) if (GUILayout.Button((actor == p.Id ? "● " : "") + p.Id + (p.Ready ? " / ready" : ""))) SelectActor(p.Id);
                if (response.View.ActivePlayer != null && GUILayout.Button("Switch to active player")) SelectActor(response.View.ActivePlayer);
                Button("Ready", "Ready"); Button("Start (host)", "Start");
                GUILayout.Space(12); GUILayout.Label("Click a hex, edge or vertex to select its ID.");
                GUILayout.Label("Target ID"); target = GUILayout.TextField(target); GUILayout.Label("Second road ID (-1 if none)"); secondTarget = GUILayout.TextField(secondTarget);
                Button("Place initial settlement", "PlaceSettlement"); Button("Place initial road", "PlaceRoad"); Button("Roll dice", "Roll");
                Button("Build road", "BuildRoad"); Button("Build settlement", "BuildSettlement"); Button("Upgrade city", "BuildCity"); Button("Buy development card", "BuyCard");
                Button("Move robber to selected hex", "MoveRobber"); GUILayout.Label("Victim / trading partner"); partner = GUILayout.TextField(partner); Button("Choose robber victim", "ChooseVictim");
                Button("End turn", "EndTurn"); Button("Disconnect selected actor", "Disconnect"); Button("Reconnect selected actor", "Reconnect"); Button("Surrender permanently", "Surrender");
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(1300,70,280,900), GUI.skin.box); rightScroll = GUILayout.BeginScrollView(rightScroll);
            if (response != null)
            {
                var view = response.View; var me = view.Players.First(p => p.Id == actor);
                GUI.enabled = true; GUILayout.Label("GAME " + view.RoomCode); GUILayout.Label("Viewing: " + actor);
                if (view.Winner != null) GUILayout.Label("WINNER: " + view.Winner);
                long now = response.ServerMilliseconds + (long)((Time.unscaledTime - receivedAt) * 1000);
                if (view.DeadlineMilliseconds.HasValue) GUILayout.Label("Phase timer: " + Math.Max(0,(view.DeadlineMilliseconds.Value-now)/1000) + "s");
                if (view.ActionDeadlineMilliseconds.HasValue) GUILayout.Label("Action timer: " + Math.Max(0,(view.ActionDeadlineMilliseconds.Value-now)/1000) + "s");
                foreach (var p in view.Players) GUILayout.Label(p.Id + ": " + p.PublicPoints + " visible VP / " + p.ResourceCount + " resources / " + p.DevelopmentCardCount + " cards" + (p.Surrendered ? " / surrendered" : !p.Connected ? " / offline" : ""));
                GUILayout.Label("Your total VP: " + me.TotalPoints); GUILayout.Label("Your resources:");
                for (int i = 0; i < resources.Length; i++) GUILayout.Label(resources[i] + ": " + me.Resources[i] + " (bank " + view.Bank[i] + ")");
                GUILayout.Label("Your cards: " + string.Join(", ", me.Cards));
                GUILayout.Label("Longest road: " + (view.LongestRoad ?? "none") + " / Army: " + (view.LargestArmy ?? "none"));
                if (view.RequiredDiscards.TryGetValue(actor, out var count)) GUILayout.Label("You must discard " + count);
                if (view.EligibleVictims.Length > 0) GUILayout.Label("Victims: " + string.Join(", ", view.EligibleVictims));
                GUI.enabled = !busy;
                GUILayout.Space(10); GUILayout.Label("Bundles: brick,lumber,wool,grain,ore"); GUILayout.Label("Give / discard"); give = GUILayout.TextField(give); GUILayout.Label("Take / Year of Plenty"); take = GUILayout.TextField(take);
                Button("Discard required resources", "Discard", true); Button("Bank / harbor trade", "BankTrade", true); Button("Offer player trade", "OfferTrade", true);
                GUILayout.Label("Offer ID"); offerId = GUILayout.TextField(offerId); Button("Accept offer", "AcceptTrade"); Button("Cancel offer", "CancelTrade");
                foreach (var trade in view.Trades) GUILayout.Label("#" + trade.Id + " " + trade.From + " → " + trade.To + "\nGive " + string.Join(",",trade.Give) + " / take " + string.Join(",",trade.Take));
                GUILayout.Label("Resource for Monopoly"); foreach (string r in resources) if (GUILayout.Button((resource == r ? "● " : "") + r)) resource = r;
                Button("Play Knight", "PlayKnight"); Button("Play Road Building", "PlayRoadBuilding"); Button("Play Year of Plenty", "PlayYearOfPlenty", true); Button("Play Monopoly", "PlayMonopoly", false, true);
                GUILayout.Label("Legal roads: " + string.Join(",", response.LegalRoads)); GUILayout.Label("Legal settlements: " + string.Join(",", response.LegalSettlements));
                GUILayout.Label("Bank ratios: " + string.Join(",", response.TradeRatios));
            }
            GUI.enabled = true; GUILayout.EndScrollView(); GUILayout.EndArea(); GUI.matrix = originalMatrix;
        }
        private void Button(string label, string kind, bool bundles = false, bool useResource = false) { if (GUILayout.Button(label)) Command(kind, bundles, useResource); }
        private void OnDestroy() { destroyed = true; transport?.Dispose(); }
    }
}
