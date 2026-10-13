using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
namespace Catan.Client
{
    public sealed class CatanPrototype : MonoBehaviour
    {
        public string DotnetExecutable="dotnet",RepositoryRoot="";
        private IGameTransport transport;
        private BoardRenderer board;
        private TabletopSkin skin;
        private HostResponse response;
        private string actor="",status="Welcome. Start a local game to play.",modal="New",dragging,partner="",chosenResource="Brick";
        private readonly string[] names={"Ryan","John","Player 3","Player 4"},resources={"Brick","Lumber","Wool","Grain","Ore"};
        private readonly int[] give=new int[5],take=new int[5];
        private int testFirst=1,testSecond=1,playerCount=2,epoch,firstRoad=-1;
        private int[] secondRoads;
        private string seed="42";
        private bool busy,polling,destroyed,freeRoads,relaxedTimers=true,testing=true,chosenDice;
        private float receivedAt,nextPoll,rollAnimationEnd,rollNoticeEnd;
        private string lastDiceKey,startingRollGame;
        private float startingRollUntil,activityHighlightUntil;
        private long lastResourceEvent;
        private bool showActivity=true;
        private Vector2 activityScroll;
        private long renderedVersion=-1,stagedVersion=-1;
        private string renderedActor,lastGame;
        private float scale;
        private Vector2 offset,mouse,tradeScroll;
        private readonly Rect viewport=new Rect(265,112,1320,550);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap(){if(FindFirstObjectByType<CatanPrototype>()==null)new GameObject("Catan table").AddComponent<CatanPrototype>();}
        private void Start()
        {
            skin=new TabletopSkin();board=GetComponent<BoardRenderer>()??gameObject.AddComponent<BoardRenderer>();
            try
            {
                string rid=Application.platform==RuntimePlatform.WindowsEditor||Application.platform==RuntimePlatform.WindowsPlayer?"win-x64":Application.platform==RuntimePlatform.OSXEditor||Application.platform==RuntimePlatform.OSXPlayer?(SystemInfo.processorType.Contains("Apple")?"osx-arm64":"osx-x64"):"linux-x64";
                string packaged=Path.Combine(Application.streamingAssetsPath,"Backend",rid,rid.StartsWith("win")?"Game.LocalHost.exe":"Game.LocalHost");
                // Editor always uses the current Release build, avoiding an outdated packaged backend.
                if(!Application.isEditor)
                {
                    if(!File.Exists(packaged))throw new FileNotFoundException("Build the desktop backend package first; see unity/README.md.");
                    transport=new LocalProcessTransport(packaged,"",Path.GetDirectoryName(packaged));
                }
                else
                {
                    string root=string.IsNullOrWhiteSpace(RepositoryRoot)?Path.GetFullPath(Path.Combine(Application.dataPath,"../../..")):RepositoryRoot;
                    string dll=Path.Combine(root,"src/Game.LocalHost/bin/Release/net10.0/Game.LocalHost.dll");
                    if(!File.Exists(dll))throw new FileNotFoundException("Run dotnet build Game.sln -c Release, then restart Play mode.");
                    string exe=DotnetExecutable;
                    if(exe=="dotnet")
                    {
                        string windows=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
                        foreach(string path in new[]{windows,"/usr/local/share/dotnet/dotnet","/usr/share/dotnet/dotnet","/workspace/.tools/dotnet/dotnet"})if(File.Exists(path)){exe=path;break;}
                    }
                    transport=new LocalProcessTransport(exe,"\""+dll+"\"",root);
                }
            }
            catch(Exception e){status="Backend could not start: "+e.Message;Debug.LogError(status);}
        }
        private void Update()
        {
            if(!busy&&!polling&&response!=null&&Time.unscaledTime>=nextPoll){nextPoll=Time.unscaledTime+.5f;_ = Refresh();}
        }
        private async Task<HostResponse> Send(HostRequest request,int token,bool announce=true,bool preview=false)
        {
            if(transport==null)throw new InvalidOperationException("Backend unavailable. Rebuild Release and restart Play mode. Check Unity Console for details.");
            var incoming=await transport.RequestAsync(request);
            if(destroyed||token!=epoch)return null;
            if(incoming.ProtocolVersion<6)throw new InvalidOperationException("The C# backend is outdated. Stop Play mode, pull feature/unity-ui, run dotnet build Game.sln -c Release, then restart Play mode.");
            if(incoming.View!=null&&!preview)
            {
                if(response!=null&&incoming.View.GameId==response.View.GameId&&request.Viewer==actor&&incoming.View.Version<response.View.Version)return incoming;
                if(response==null||incoming.View.GameId!=response.View.GameId){lastResourceEvent=0;activityScroll=Vector2.zero;}
                actor=request.Viewer;response=incoming;receivedAt=Time.unscaledTime;
                var latest=incoming.View.ResourceActivity?.LastOrDefault();
                if(latest!=null&&latest.Id>lastResourceEvent){lastResourceEvent=latest.Id;activityHighlightUntil=Time.unscaledTime+10;showActivity=true;activityScroll=Vector2.zero;}
                if(incoming.View.Turn==0&&incoming.View.StartingRolls!=null&&incoming.View.StartingRolls.Length>0&&startingRollGame!=incoming.View.GameId){startingRollGame=incoming.View.GameId;startingRollUntil=Time.unscaledTime+6;}
                if(incoming.View.Dice!=null)
                {
                    string key=incoming.View.GameId+":"+incoming.View.Turn;
                    if(key!=lastDiceKey){lastDiceKey=key;rollAnimationEnd=Time.unscaledTime+.9f;rollNoticeEnd=Time.unscaledTime+7;}
                }
                if((firstRoad>=0&&(incoming.View.Version!=stagedVersion||actor!=renderedActor))||(freeRoads&&(incoming.View.ActivePlayer!=actor||(incoming.View.Phase!="Action"&&incoming.View.Phase!="AwaitRoll")))){CancelFreeRoads();}
                if(incoming.View.Version!=renderedVersion||actor!=renderedActor||incoming.View.GameId!=lastGame)
                {board.Render(incoming);renderedVersion=incoming.View.Version;renderedActor=actor;lastGame=incoming.View.GameId;}
                if(modal=="Discard"&&(incoming.View.Phase!="Discard"||!incoming.View.RequiredDiscards.ContainsKey(actor)))modal="";
                if(incoming.View.Phase=="Discard"&&incoming.View.RequiredDiscards.ContainsKey(actor)&&modal!="Discard"){ResetBundles();modal="Discard";}
            }
            if(announce||!incoming.Success)status=incoming.Success?"Done. "+(incoming.View==null?"":ClientActions.Instruction(incoming.View.Phase)):incoming.Message;
            return incoming;
        }
        private async Task Refresh()
        {
            polling=true;int token=epoch;
            try{await Send(new HostRequest{Op="view",Viewer=actor},token,false);}
            catch(Exception e){if(!destroyed){status=e.Message;nextPoll=Time.unscaledTime+5;}}
            finally{polling=false;}
        }
        private async void NewGame()
        {
            if(busy)return;busy=true;int token=++epoch;CancelFreeRoads();
            try
            {
                var players=names.Take(playerCount).Select(n=>n.Trim()).ToArray();
                var created=await Send(new HostRequest{Op="create",Viewer=players[0],Players=players,Seed=ulong.Parse(seed),RelaxedTimers=relaxedTimers,Testing=testing},token);
                if(created==null||!created.Success)return;
                foreach(string p in players){var ready=await Send(new HostRequest{Op="command",Viewer=p,Command=Intent("Ready",p,1)},token,false);if(ready==null||!ready.Success)return;}
                var start=await Send(new HostRequest{Op="command",Viewer=players[0],Command=Intent("Start",players[0])},token);
                if(start!=null&&start.Success){modal="";partner="";}
            }
            catch(Exception e){status=e.Message;}
            finally{busy=false;}
        }
        private async void SelectActor(string name,bool reviewTrade=false)
        {
            if(busy)return;busy=true;int token=++epoch;dragging=null;CancelFreeRoads();
            try{modal="";ResetBundles();await Send(new HostRequest{Op="view",Viewer=name},token,false);if(reviewTrade)modal="Trade";}
            catch(Exception e){status=e.Message;}
            finally{busy=false;}
        }
        private static Intent Intent(string kind,string player,int target=-1)=>new Intent{Kind=kind,Player=player,Target=target,RequestId=Guid.NewGuid().ToString("N")};
        private async void Execute(Intent intent,bool close=true)
        {
            if(busy)return;busy=true;int token=++epoch;
            try{var result=await Send(new HostRequest{Op="command",Viewer=actor,Command=intent},token);if(result!=null&&result.Success){if(close)modal="";if(intent.Kind=="OfferTrade")status="Offer sent. Choose Review as the other player, then Accept. Keep the current turn open.";}}
            catch(Exception e){status=e.Message;}
            finally{busy=false;}
        }
        private void CancelFreeRoads(){firstRoad=-1;secondRoads=null;freeRoads=false;stagedVersion=-1;board?.Focus(null);}
        private async void Drop(string piece,int id)
        {
            if(busy||id<0){status="Drop onto a glowing location on the board.";return;}
            if(piece=="Road"&&freeRoads)
            {
                if(response.View.ActivePlayer!=actor){CancelFreeRoads();return;}
                if(firstRoad>=0)
                {
                    if(!ClientActions.Contains(secondRoads,id)){status="Choose a glowing edge for your second free road.";return;}
                    var play=Intent("PlayRoadBuilding",actor,firstRoad);play.SecondTarget=id;CancelFreeRoads();Execute(play);return;
                }
                busy=true;int token=++epoch;
                try
                {
                    var preview=await Send(new HostRequest{Op="preview-roads",Viewer=actor,Command=Intent("PlayRoadBuilding",actor,id)},token,true,true);
                    if(preview==null||!preview.Success)return;
                    if(preview.LegalRoads.Length==0){freeRoads=false;busy=false;Execute(Intent("PlayRoadBuilding",actor,id));return;}
                    firstRoad=id;secondRoads=preview.LegalRoads;stagedVersion=preview.View.Version;
                    board.Focus("Road",secondRoads,firstRoad);status="First free road staged. Drag the road icon again to place the second; cancel to keep your card.";
                }
                catch(Exception e){status=e.Message;}
                finally{busy=false;}
                return;
            }
            var command=ClientActions.Drop(response,actor,piece,id);
            if(command==null){status=piece=="Road"?(response.View.Turn==0?"In initial setup, each road must touch the settlement you just placed.":"Roads must join your own road or settlement, without crossing an opponent’s building. Roll first, then build."):piece=="Settlement"?"Leave an empty intersection between all settlements, including your own. During normal turns, connect to your road.":"That drop is not available. Use a glowing location during your turn.";return;}
            Execute(command);
        }
        private bool MyTurn=>response!=null&&response.View.ActivePlayer==actor;
        private Participant Me=>response?.View.Players.FirstOrDefault(p=>p.Id==actor);
        private void ResetBundles(){Array.Clear(give,0,5);Array.Clear(take,0,5);}
        private void Open(string dialog){ResetBundles();modal=dialog;if(response!=null&&(partner==actor||(!string.IsNullOrEmpty(partner)&&!response.View.Players.Any(p=>p.Id==partner))))partner="";}
        private void OnGUI()
        {
            if(skin==null||board==null)return;
            scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);offset=new Vector2((Screen.width-1600*scale)/2,(Screen.height-900*scale)/2);
            board.SetViewport(new Rect(offset.x+viewport.x*scale,offset.y+viewport.y*scale,viewport.width*scale,viewport.height*scale));
            var old=GUI.matrix;mouse=(Event.current.mousePosition-offset)/scale;GUI.matrix=Matrix4x4.TRS(offset,Quaternion.identity,Vector3.one*scale);
            GUI.enabled=modal=="";
            skin.Box(new Rect(0,0,1600,105),skin.Background);skin.Box(new Rect(0,105,260,795),skin.Background);skin.Box(new Rect(260,665,1340,235),skin.Background);
            Header();Players();Hand();Tray();
            if(response!=null)
            {
                skin.Text(new Rect(300,116,1260,50),ClientActions.Instruction(response.View.Phase),24,skin.Ink,TextAnchor.MiddleCenter);
                if(!MyTurn&&response.View.Phase!="Finished"&&response.View.Phase!="Abandoned"&&response.View.Phase!="Discard")
                {
                    skin.Box(new Rect(620,315,600,155),skin.Panel);skin.Text(new Rect(645,330,550,55),"Pass the table to "+response.View.ActivePlayer,28,skin.Ink,TextAnchor.MiddleCenter);
                    if(skin.Button(new Rect(730,395,380,52),"Continue as "+response.View.ActivePlayer,!busy,true))SelectActor(response.View.ActivePlayer);
                }
                if(response.View.Dice!=null&&Time.unscaledTime<rollNoticeEnd&&modal==""&&response.View.Phase!="Victim")DiceNotice();
                if(response.View.Turn==0&&response.View.StartingRolls!=null&&Time.unscaledTime<startingRollUntil&&modal=="")StartingDice();
                if(response.View.Phase=="Handover"&&MyTurn&&modal=="")
                {
                    skin.Box(new Rect(620,315,600,160),skin.Panel);
                    skin.Text(new Rect(645,330,550,50),"Your turn is coming — take your time",25,skin.Ink,TextAnchor.MiddleCenter);
                    if(skin.Button(new Rect(730,400,380,52),"I’m ready — start now",!busy,true))Execute(Intent("ContinueTurn",actor));
                }
                if(modal=="")ResourceActivity();
                if(response.View.Phase=="Victim"&&MyTurn&&modal=="")Victims();
                if(response.View.Winner!=null){skin.Box(new Rect(640,310,580,160),skin.Panel);skin.Text(new Rect(660,330,540,100),response.View.Winner+" wins!",40,skin.Gold,TextAnchor.MiddleCenter);}
            }
            skin.Text(new Rect(285,876,1285,24),status,18,skin.Muted);
            GUI.enabled=true;
            if(modal=="New")NewDialog();else if(modal=="Cards")Cards();else if(modal=="Trade"||modal=="Discard"||modal=="Plenty"||modal=="Monopoly")ResourceDialog();
            DragEvents();GUI.matrix=old;
        }
        private void ResourceActivity()
        {
            var entries=response.View.ResourceActivity;
            if(entries==null||entries.Length==0)return;
            bool fresh=Time.unscaledTime<activityHighlightUntil;
            if(skin.Button(new Rect(1235,359,340,34),(fresh?"New resources • ":"")+"Activity "+(showActivity?"−":"+")))showActivity=!showActivity;
            if(!showActivity)return;
            skin.Box(new Rect(1235,395,340,262),skin.Panel);
            var recent=entries.Reverse().Take(40).ToArray();
            activityScroll=GUI.BeginScrollView(new Rect(1240,400,330,250),activityScroll,new Rect(0,0,308,recent.Length*78));
            for(int i=0;i<recent.Length;i++)
            {
                var entry=recent[i];float y=i*78;
                skin.Text(new Rect(6,y,294,25),entry.Player+" • "+(entry.Source=="Starting resources"?"Second settlement":"Rolled "+entry.Roll),15,BoardRenderer.PlayerColor(response.View,entry.Player));
                int total=entry.Received.Sum();
                if(total==0)skin.Text(new Rect(6,y+27,294,38),entry.Roll==7?"No production on 7":"No resources received",14,skin.Muted);
                else
                {
                    float x=6;for(int r=0;r<5;r++)if(entry.Received[r]>0)
                    {skin.Icon(new Rect(x,y+29,24,24),resources[r],ResourceColor(r));skin.Text(new Rect(x+28,y+27,28,28),"+"+entry.Received[r],15,skin.Gold);x+=60;}
                    skin.Text(new Rect(6,y+54,294,21),entry.Source=="Starting resources"?"Starting resources added to hand":"Resources added to hand",12,skin.Muted);
                }
                skin.Box(new Rect(6,y+74,294,1),skin.Muted*.45f);
            }
            GUI.EndScrollView();
        }
        private void StartingDice()
        {
            var v=response.View;var rolls=v.StartingRolls.GroupBy(r=>r.Player).Select(g=>g.Last()).ToArray();
            if(rolls.Length==0)return;
            skin.Box(new Rect(300,176,480,45+rolls.Length*40),skin.Panel);
            if(skin.Button(new Rect(710,181,58,27),"Hide"))startingRollUntil=0;
            skin.Text(new Rect(320,180,445,35),"Starting roll-off — "+v.StartingPlayer+" goes first",19,skin.Gold);
            for(int i=0;i<rolls.Length;i++)
            {
                var roll=rolls[i];float y=220+i*40;
                skin.Text(new Rect(320,y,180,33),roll.Player+" (round "+roll.Round+")",16);
                skin.Die(new Rect(505,y,30,30),roll.First);skin.Die(new Rect(545,y,30,30),roll.Second);
                skin.Text(new Rect(592,y,120,33),"= "+(roll.First+roll.Second),20);
            }
        }
        private void DiceNotice()
        {
            var dice=response.View.Dice;bool rolling=Time.unscaledTime<rollAnimationEnd;
            skin.Box(new Rect(770,170,640,175),skin.Panel);
            int first=rolling?1+(int)(Time.unscaledTime*22)%6:dice.First;
            int second=rolling?1+(int)(Time.unscaledTime*29+2)%6:dice.Second;
            float wobble=rolling?Mathf.Sin(Time.unscaledTime*28)*7:0;
            skin.Die(new Rect(795,195+wobble,64,64),first,rolling);skin.Die(new Rect(875,195-wobble,64,64),second,rolling);
            skin.Text(new Rect(965,185,420,47),rolling?"Rolling…":"Rolled "+dice.First+" + "+dice.Second+" = "+dice.Total,25,skin.Gold);
            if(rolling)return;
            if(dice.Total==7){skin.Text(new Rect(795,276,590,48),"No production on 7 — resolve discards and the robber.",19);return;}
            int total=dice.ResourcesReceived.Sum();
            skin.Text(new Rect(965,232,420,38),actor+(total==0?" received no resources.":" received "+total+" resources:"),19);
            float x=795;for(int r=0;r<5;r++)if(dice.ResourcesReceived[r]>0){skin.Icon(new Rect(x,282,30,30),resources[r],ResourceColor(r));skin.Text(new Rect(x+35,280,110,34),"+"+dice.ResourcesReceived[r]+" "+resources[r],16);x+=122;}
        }
        private void Header()
        {
            skin.Text(new Rect(26,20,230,60),"CATAN",34,skin.Gold);
            skin.Text(new Rect(27,76,220,23),"V2 • UI build 0.8",14,skin.Muted);
            if(response==null){skin.Text(new Rect(300,22,800,60),"Local hot-seat • Unity desktop",24);return;}
            var v=response.View;
            skin.Text(new Rect(300,18,790,35),(v.Turn==0?"INITIAL SETUP":"TURN "+v.Turn)+"   /   "+(v.ActivePlayer??"Lobby"),25,BoardRenderer.PlayerColor(v,v.ActivePlayer));
            string detail=v.Turn==0?"Settlements "+v.Vertices.Count(p=>p.Level>0)+"/"+(v.Players.Length*2)+"  •  Roads "+v.Edges.Count(e=>e.Owner!=null)+"/"+(v.Players.Length*2)+" — dice turns follow setup":"Dice total: "+(v.LastRoll.HasValue?v.LastRoll.Value.ToString():"—")+"   •   "+PhaseLabel(v.Phase);
            skin.Text(new Rect(300,55,650,32),detail,17,skin.Muted);
            if(v.Dice!=null){skin.Die(new Rect(970,28,42,42),v.Dice.First);skin.Die(new Rect(1025,28,42,42),v.Dice.Second);}
            double rem=ClientActions.Remaining(response,Time.unscaledTime-receivedAt);
            double duration=v.Phase=="Action"?(response.Timing?.ActionMilliseconds??90_000)/1000.0:v.Phase=="AwaitRoll"?(response.Timing?.RollMilliseconds??15_000)/1000.0:v.Phase=="Handover"?(response.Timing?.HandoverMilliseconds??30_000)/1000.0:v.Phase=="ReconnectGrace"?10:(response.Timing?.MandatoryMilliseconds??30_000)/1000.0;
            skin.Timer(new Rect(1130,8,86,86),rem,duration);
            skin.Text(new Rect(1227,19,140,58),"TIME LEFT",16,skin.Muted);
            if(skin.Button(new Rect(1390,24,180,52),"New game",!busy)){modal="New";}
        }
        private static string PhaseLabel(string phase){switch(phase){case "Handover":return "Handover break";case "SetupSettlement":case "SetupRoad":return "Initial placement";case "AwaitRoll":return "Roll phase";case "Action":return "Build & trade";case "Discard":return "Discard resources";case "Robber":case "Victim":return "Robber";default:return phase;}}
        private void Players()
        {
            if(response==null)return;var v=response.View;skin.Text(new Rect(22,120,215,40),"AT THE TABLE",17,skin.Muted);
            for(int i=0;i<v.Players.Length;i++)
            {
                var p=v.Players[i];float y=170+i*108;skin.Box(new Rect(18,y,224,106),p.Id==actor?new Color(.14f,.25f,.27f):skin.Panel);
                skin.Box(new Rect(18,y,5,106),BoardRenderer.PlayerColor(v,p.Id));
                if(skin.Button(new Rect(35,y+9,190,32),p.Id+ (p.Id==v.ActivePlayer?" •":""),!busy))SelectActor(p.Id);
                skin.Text(new Rect(35,y+44,200,24),p.PublicPoints+" VP    "+p.ResourceCount+" resources",16,skin.Muted);
                skin.Text(new Rect(35,y+67,200,24),v.Dice!=null&&v.Dice.PlayerResourceCounts[p.Id]>0?"+"+v.Dice.PlayerResourceCounts[p.Id]+" from the roll":p.Surrendered?"Surrendered":!p.Connected?"Disconnected":p.DevelopmentCardCount+" development cards",14,skin.Muted);
                if(response.TableResources!=null&&response.TableResources.TryGetValue(p.Id,out var counts))
                    for(int r=0;r<5;r++){float x=30+r*42;skin.Icon(new Rect(x,y+88,15,15),resources[r],ResourceColor(r));skin.Text(new Rect(x+17,y+86,25,20),counts[r].ToString(),12);}
                if(v.RequiredDiscards.ContainsKey(p.Id))skin.Text(new Rect(35,y+44,200,24),"Must discard "+v.RequiredDiscards[p.Id],16,skin.Gold);
            }
            skin.Text(new Rect(22,620,220,60),"Longest road\n"+(v.LongestRoad??"Unclaimed"),17,skin.Muted);skin.Text(new Rect(22,697,220,60),"Largest army\n"+(v.LargestArmy??"Unclaimed"),17,skin.Muted);
            if(Me!=null&&Me.Cards.Length>0&&skin.Button(new Rect(22,791,220,52),"Development cards",!busy))modal="Cards";
        }
        private void Hand()
        {
            if(Me==null)return;
            string receipt=response.View.Dice==null?"":response.View.Dice.Total==7?"  •  Rolled 7: no production":"  •  Last roll: "+(response.View.Dice.ResourcesReceived.Sum()==0?"no resources":BundleText(response.View.Dice.ResourcesReceived));
            skin.Text(new Rect(285,675,900,28),actor+"’s hand   /   "+Me.TotalPoints+" total VP"+receipt,17,BoardRenderer.PlayerColor(response.View,actor));
            for(int i=0;i<5;i++)
            {
                float x=285+i*172;skin.Box(new Rect(x,714,156,70),skin.Panel);skin.Icon(new Rect(x+10,728,42,42),resources[i],ResourceColor(i));
                skin.Text(new Rect(x+63,715,80,35),Me.Resources[i].ToString(),27);int gained=response.View.Dice?.ResourcesReceived[i]??0;skin.Text(new Rect(x+63,752,85,25),resources[i]+(gained>0?" +"+gained:""),15,gained>0?skin.Gold:skin.Muted);
            }
            if(response.Testing)
            {
                chosenDice=GUI.Toggle(new Rect(1195,858,155,28),chosenDice,"Choose test dice");
                if(skin.Button(new Rect(1360,852,100,34),"Die 1: "+testFirst,!busy))testFirst=testFirst%6+1;
                if(skin.Button(new Rect(1470,852,100,34),"Die 2: "+testSecond,!busy))testSecond=testSecond%6+1;
            }
            bool offers=response.View.Trades.Any(t=>t.From!=actor&&(t.To==actor||t.To==null));
            if(skin.Button(new Rect(1195,710,180,70),"Roll dice",!busy&&MyTurn&&response.View.Phase=="AwaitRoll",true)){var roll=Intent("Roll",actor);if(response.Testing&&chosenDice){roll.FirstDie=testFirst;roll.SecondDie=testSecond;}Execute(roll);}
            skin.Icon(new Rect(1204,726,26,26),"Dice",skin.Background);
            if(skin.Button(new Rect(1390,710,180,70),offers?"Trade offer":"Trade",!busy&&response.View.Phase=="Action"&&(MyTurn||offers)))Open("Trade");
            if(skin.Button(new Rect(1390,796,180,50),"End turn",!busy&&MyTurn&&response.View.Phase=="Action"))Execute(Intent("EndTurn",actor));
        }
        private void Tray()
        {
            if(response==null)return;
            string[] pieces=response.View.Phase=="Robber"?new[]{"Robber"}:new[]{"Settlement","Road","City"};
            for(int i=0;i<pieces.Length;i++)
            {
                string piece=pieces[i];float x=285+i*230;var rect=new Rect(x,800,218,52);
                int used=piece=="Road"?response.View.Edges.Count(e=>e.Owner==actor):response.View.Vertices.Count(v=>v.Owner==actor&&v.Level==(piece=="City"?2:1));
                int left=piece=="Road"?15-used:piece=="City"?4-used:5-used;
                bool enabled=!busy&&MyTurn&&(piece=="Robber"?response.View.Phase=="Robber":piece=="Road"?response.View.Phase=="SetupRoad"||response.View.Phase=="Action"||freeRoads:piece=="Settlement"?response.View.Phase=="SetupSettlement"||response.View.Phase=="Action":response.View.Phase=="Action");
                bool affordable=piece=="Robber"||response.View.Phase!="Action"||(piece=="Road"&&freeRoads)||ClientActions.CanAfford(response,actor,piece);
                enabled=enabled&&affordable&&(piece=="Robber"||left>0);
                skin.Box(rect,enabled?new Color(.18f,.30f,.32f):skin.Panel);skin.Icon(new Rect(x+10,804,42,42),piece,BoardRenderer.PlayerColor(response.View,actor));
                skin.Text(new Rect(x+60,801,150,30),piece+(piece=="Robber"?"":" ×"+left),18,skin.Ink);
                if(piece!="Robber"&&response.View.Turn>0)
                {
                    CostIcons(x+60,829,piece);
                    var missing=ClientActions.Missing(response,actor,piece);
                    skin.Text(new Rect(x,847,225,18),piece=="Road"&&freeRoads?"Free road card":missing==null?"Costs unavailable":missing.Sum()>0?"Need "+BundleText(missing):left==0?"No pieces left":"Resources ready",12,missing!=null&&missing.Sum()>0?skin.Gold:skin.Muted);
                }
                if(enabled&&modal==""&&Event.current.type==EventType.MouseDown&&Event.current.button==0&&rect.Contains(mouse))
                {dragging=piece;board.Focus(piece,freeRoads?secondRoads:null,firstRoad);Event.current.Use();}
            }
            if(freeRoads){skin.Text(new Rect(995,798,370,50),firstRoad<0?"Drag two free roads":"Drag your second free road",18,skin.Gold);if(skin.Button(new Rect(995,843,200,30),"Cancel free roads",!busy)){CancelFreeRoads();}}
            else if(response.View.Phase=="Action")
            {
                if(skin.Button(new Rect(995,800,380,40),"Buy development card",!busy&&MyTurn&&ClientActions.CanAfford(response,actor,"Development")))Execute(Intent("BuyCard",actor));
                var missing=ClientActions.Missing(response,actor,"Development");skin.Text(new Rect(995,841,380,20),missing!=null&&missing.Sum()>0?"Need "+BundleText(missing):"Resources ready",13,skin.Gold);
            }
        }
        private void CostIcons(float x,float y,string piece)
        {
            if(response.Costs==null||!response.Costs.TryGetValue(piece,out var cost))return;
            for(int r=0;r<5;r++)if(cost[r]>0){skin.Icon(new Rect(x,y,14,14),resources[r],ResourceColor(r));skin.Text(new Rect(x+15,y-2,15,18),cost[r].ToString(),12,Me.Resources[r]<cost[r]?skin.Gold:skin.Muted);x+=31;}
        }
        private void DragEvents()
        {
            if(dragging==null)return;
            skin.Icon(new Rect(mouse.x-32,mouse.y-32,64,64),dragging,BoardRenderer.PlayerColor(response.View,actor));
            if(Event.current.type==EventType.MouseDrag){Event.current.Use();}
            if(Event.current.type==EventType.MouseUp&&Event.current.button==0)
            {
                string piece=dragging;dragging=null;Vector2 physical=new Vector2(offset.x+mouse.x*scale,Screen.height-offset.y-mouse.y*scale);
                int id=board.Pick(physical,piece=="Road"?"Edge":piece=="Robber"?"Hex":"Vertex");
                board.Focus(freeRoads?"Road":null,secondRoads,firstRoad);Drop(piece,id);Event.current.Use();
            }
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){dragging=null;board.Focus(freeRoads?"Road":null,secondRoads,firstRoad);Event.current.Use();}
        }
        private void Overlay(Rect rect,string title)
        {
            skin.Box(new Rect(0,0,1600,900),new Color(0,0,0,.65f));skin.Box(rect,skin.Panel);skin.Text(new Rect(rect.x+30,rect.y+18,rect.width-60,48),title,28,skin.Gold);
        }
        private void NewDialog()
        {
            Overlay(new Rect(500,155,600,600),"Start a local game");skin.Text(new Rect(535,225,530,42),"Pass-and-play on this computer",20,skin.Muted);
            for(int n=2;n<=4;n++)if(skin.Button(new Rect(535+(n-2)*175,282,158,48),n+" players"+(playerCount==n?" ✓":""),!busy))playerCount=n;
            Color[] seats={new Color(.93f,.38f,.28f),new Color(.26f,.65f,.96f),new Color(.95f,.78f,.28f),new Color(.83f,.79f,.93f)};
            for(int i=0;i<playerCount;i++){skin.Icon(new Rect(535,355+i*58,38,38),"Settlement",seats[i]);skin.Text(new Rect(580,352+i*58,95,42),"Player "+(i+1),18,seats[i]);names[i]=skin.Field(new Rect(690,352+i*58,365,42),names[i]);}
            skin.Text(new Rect(535,605,130,40),"Board seed",18,skin.Muted);seed=skin.Field(new Rect(675,605,180,40),seed);
            relaxedTimers=GUI.Toggle(new Rect(875,615,205,35),relaxedTimers,"Relaxed testing pace");
            testing=GUI.Toggle(new Rect(535,650,500,24),testing,"Testing: show all resources and allow chosen dice");
            if(skin.Button(new Rect(535,674,355,54),busy?"Starting…":"Start game",!busy&&transport!=null,true))NewGame();
            if(response!=null&&skin.Button(new Rect(905,674,150,54),"Cancel",!busy))modal="";
            skin.Text(new Rect(510,760,580,95),status,17,skin.Ink,TextAnchor.MiddleCenter);
        }
        private void Victims()
        {
            Overlay(new Rect(600,265,620,330),"Choose a player to steal from");
            for(int i=0;i<response.View.EligibleVictims.Length;i++){string p=response.View.EligibleVictims[i];if(skin.Button(new Rect(645,350+i*65,530,50),p,!busy)){var cmd=Intent("ChooseVictim",actor);cmd.OtherPlayer=p;Execute(cmd);}}
        }
        private void ResourceDialog()
        {
            bool trade=modal=="Trade",discard=modal=="Discard",monopoly=modal=="Monopoly";
            Overlay(new Rect(470,140,910,655),discard?"Discard "+response.View.RequiredDiscards[actor]+" resources":trade?"Trade resources":monopoly?"Choose a resource":"Choose your two resources");
            skin.Text(new Rect(510,210,315,34),trade?"Viewing "+actor+" • Review as:":"Timer keeps running while you choose.",16,skin.Muted);
            if(trade){float seatX=830;foreach(var p in response.View.Players.Where(p=>p.Id!=actor)){if(skin.Button(new Rect(seatX,210,158,34),p.Id,!busy))SelectActor(p.Id,true);seatX+=165;}}
            if(trade)
            {
                skin.Text(new Rect(510,260,130,35),"Partner",18);float x=650;
                if(skin.Button(new Rect(x,254,152,40),"Anyone"+(partner==""?" ✓":""),!busy))partner="";x+=160;
                foreach(var p in response.View.Players.Where(p=>p.Id!=actor)){if(skin.Button(new Rect(x,254,152,40),p.Id+(partner==p.Id?" ✓":""),!busy))partner=p.Id;x+=160;}
            }
            skin.Text(new Rect(820,302,185,30),discard?"Discard":"Give",18,skin.Muted);skin.Text(new Rect(1080,302,190,30),"Receive",18,skin.Muted);
            for(int i=0;i<5;i++)
            {
                float y=345+i*58;skin.Icon(new Rect(510,y,40,40),resources[i],ResourceColor(i));skin.Text(new Rect(562,y,215,40),resources[i]+(trade?"  "+response.TradeRatios[i]+":1":""),20);
                if(monopoly){if(skin.Button(new Rect(820,y,420,40),"Take all "+resources[i],!busy)){var cmd=Intent("PlayMonopoly",actor);cmd.Resource=resources[i];Execute(cmd);}continue;}
                if(trade||discard)Stepper(815,y,give,i,Me.Resources[i]);
                if(!discard)Stepper(1070,y,take,i,trade?19:response.View.Bank[i]);
            }
            if(discard){if(skin.Button(new Rect(515,660,390,54),"Discard selected ("+give.Sum()+")",!busy,true)){var cmd=Intent("Discard",actor);cmd.Give=(int[])give.Clone();Execute(cmd);}}
            else if(modal=="Plenty"){if(skin.Button(new Rect(515,660,390,54),"Take selected ("+take.Sum()+")",!busy,true)){var cmd=Intent("PlayYearOfPlenty",actor);cmd.Take=(int[])take.Clone();Execute(cmd);}}
            else if(trade)
            {
                if(skin.Button(new Rect(515,660,260,50),"Bank / port trade",!busy&&MyTurn,true))Trade("BankTrade");
                if(skin.Button(new Rect(790,660,260,50),"Offer to "+(string.IsNullOrEmpty(partner)?"anyone":partner),!busy&&MyTurn))Trade("OfferTrade");
                var available=response.View.Trades.Where(t=>t.To==actor||t.From==actor||t.To==null).ToArray();
                tradeScroll=GUI.BeginScrollView(new Rect(510,718,820,75),tradeScroll,new Rect(0,0,800,Math.Max(75,available.Length*70)));
                int row=0;foreach(var offer in available)
                {
                    float y=row*70;skin.Text(new Rect(0,y,500,36),offer.From+" gives "+BundleText(offer.Give)+" for "+BundleText(offer.Take),14);
                    if(offer.From!=actor&&(offer.To==actor||offer.To==null)&&skin.Button(new Rect(510,y,135,35),ClientActions.CanAccept(response,actor,offer)?"Accept":"Need cards",!busy&&ClientActions.CanAccept(response,actor,offer))){Execute(Intent("AcceptTrade",actor,offer.Id));}
                    if((offer.From==actor||offer.To==actor)&&skin.Button(new Rect(655,y,135,35),"Cancel offer",!busy))Execute(Intent("CancelTrade",actor,offer.Id));
                    if(offer.From!=actor&&(offer.To==actor||offer.To==null))
                    {
                        var missing=offer.Take.Select((n,i)=>Math.Max(0,n-Me.Resources[i])).ToArray();
                        skin.Text(new Rect(0,y+37,790,27),missing.Sum()>0?"You need "+BundleText(missing)+" to accept":"You can accept this exchange during "+response.View.ActivePlayer+"’s turn.",13,missing.Sum()>0?skin.Gold:skin.Muted);
                    }
                    row++;
                }
                GUI.EndScrollView();
            }
            if(!discard&&skin.Button(new Rect(1090,660,240,50),"Close",!busy))modal="";
            skin.Text(new Rect(490,805,870,45),status,18,skin.Ink,TextAnchor.MiddleCenter);
        }
        private void Stepper(float x,float y,int[] values,int i,int limit)
        {
            if(skin.Button(new Rect(x,y,45,40),"−",!busy&&values[i]>0))values[i]--;
            skin.Text(new Rect(x+50,y,58,40),values[i].ToString(),23,skin.Ink,TextAnchor.MiddleCenter);
            if(skin.Button(new Rect(x+115,y,45,40),"+",!busy&&values[i]<limit))values[i]++;
        }
        private void Trade(string kind){var cmd=Intent(kind,actor);cmd.OtherPlayer=string.IsNullOrEmpty(partner)?null:partner;cmd.Give=(int[])give.Clone();cmd.Take=(int[])take.Clone();Execute(cmd,kind!="OfferTrade");}
        private string BundleText(int[] bundle)=>string.Join(", ",resources.Where((r,i)=>bundle[i]>0).Select(r=>bundle[Array.IndexOf(resources,r)]+" "+r));
        private void Cards()
        {
            Overlay(new Rect(540,200,740,500),"Your development cards");int row=0;
            foreach(string card in Me.Cards.Distinct())
            {
                string held=card;skin.Text(new Rect(575,285+row*65,320,50),card+" ×"+Me.Cards.Count(c=>c==card),21);
                if(card!="VictoryPoint"&&skin.Button(new Rect(920,285+row*65,310,50),"Play",!busy&&MyTurn))
                {
                    if(held=="RoadBuilding"){modal="";freeRoads=true;firstRoad=-1;secondRoads=null;board.Focus("Road");status="Drag a road from the tray to begin two free roads.";}
                    else if(held=="YearOfPlenty")Open("Plenty");else if(held=="Monopoly")Open("Monopoly");else Execute(Intent("PlayKnight",actor));
                }
                row++;
            }
            skin.Text(new Rect(575,615,655,32),"Cards bought this turn cannot be played. One action card per turn.",16,skin.Muted);
            if(skin.Button(new Rect(920,650,310,40),"Close",!busy))modal="";
        }
        private Color ResourceColor(int i){Color[] colors={new Color(.96f,.52f,.37f),new Color(.4f,.8f,.56f),new Color(.77f,.88f,.61f),new Color(.97f,.81f,.37f),new Color(.65f,.77f,.9f)};return colors[i];}
        private void OnDestroy(){destroyed=true;transport?.Dispose();skin?.Dispose();}
    }
}
