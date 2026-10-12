using Catan.Client;
using Xunit;
namespace Game.Engine.Tests;

public sealed class UnityTransportTests
{
    [Fact]
    public async Task UnityWireAndProcessTransportCanCreateReadyStartAndPreservePrivacy()
    {
        string root = Repository(); string dll = Path.Combine(root, "src/Game.LocalHost/bin/Release/net10.0/Game.LocalHost.dll");
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        using var transport = new LocalProcessTransport(dotnet, "\"" + dll + "\"", root);
        var create = await transport.RequestAsync(new HostRequest { Op = "create", Viewer = "Ryan", Players = ["Ryan", "John"], Seed = 42 });
        Assert.True(create.Success); Assert.Equal("Lobby", create.View.Phase); Assert.NotNull(create.View.Players[0].Resources); Assert.Null(create.View.Players[1].Resources);
        foreach (string p in new[] { "Ryan", "John" })
        {
            var ready = await transport.RequestAsync(new HostRequest { Op = "command", Viewer = p, Command = new Intent { Kind = "Ready", Player = p, Target = 1, RequestId = Guid.NewGuid().ToString() } });
            Assert.True(ready.Success, ready.Message);
        }
        var start = await transport.RequestAsync(new HostRequest { Op = "command", Viewer = "Ryan", Command = new Intent { Kind = "Start", Player = "Ryan", RequestId = "start" } });
        Assert.True(start.Success); Assert.Equal("SetupSettlement", start.View.Phase); Assert.Equal(19, start.View.Hexes.Length); Assert.Equal(72, start.View.Edges.Length);
        Assert.NotEmpty(start.LegalSettlements); Assert.Equal(5, start.TradeRatios.Length);
        Assert.Equal(Game.Engine.Costs.Settlement,start.Costs["Settlement"]); Assert.NotNull(start.LegalCities);
        var snapshot = await transport.RequestAsync(new HostRequest { Op = "view", Viewer = start.View.ActivePlayer });
        var placed = await transport.RequestAsync(new HostRequest { Op = "command", Viewer = start.View.ActivePlayer,
            Command = ClientActions.Drop(snapshot,start.View.ActivePlayer,"Settlement",snapshot.LegalSettlements[0]) });
        Assert.True(placed.Success, placed.Message); Assert.Equal("SetupRoad", placed.View.Phase); Assert.NotEmpty(placed.LegalRoads);
        var road=ClientActions.Drop(placed,placed.View.ActivePlayer,"Road",placed.LegalRoads[0]);
        var built=await transport.RequestAsync(new HostRequest{Op="command",Viewer=placed.View.ActivePlayer,Command=road});
        Assert.True(built.Success,built.Message);Assert.Contains(built.View.Edges,e=>e.Id==road.Target&&e.Owner==road.Player);
    }
    [Fact]
    public async Task CompleteRelaxedDragSetupPlacesBothRoadsThenProducesVisibleDice()
    {
        string root=Repository();string dll=Path.Combine(root,"src/Game.LocalHost/bin/Release/net10.0/Game.LocalHost.dll");
        using var transport=new LocalProcessTransport(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet","\""+dll+"\"",root);
        var state=await transport.RequestAsync(new HostRequest{Op="create",Viewer="Ryan",Players=["Ryan","John"],Seed=42,RelaxedTimers=true});
        foreach(string p in new[]{"Ryan","John"})state=await transport.RequestAsync(new HostRequest{Op="command",Viewer=p,Command=new Intent{Kind="Ready",Player=p,Target=1}});
        state=await transport.RequestAsync(new HostRequest{Op="command",Viewer="Ryan",Command=new Intent{Kind="Start",Player="Ryan"}});
        Assert.Equal(5,state.ProtocolVersion);Assert.NotEmpty(state.View.StartingRolls);
        while(state.View.Turn==0)
        {
            string p=state.View.ActivePlayer;
            state=await transport.RequestAsync(new HostRequest{Op="view",Viewer=p});
            Intent intent;
            if(state.View.Phase=="Handover")intent=new Intent{Kind="ContinueTurn",Player=p};
            else if(state.View.Phase=="SetupSettlement")intent=ClientActions.Drop(state,p,"Settlement",state.LegalSettlements[0]);
            else intent=ClientActions.Drop(state,p,"Road",state.LegalRoads[0]);
            Assert.NotNull(intent);state=await transport.RequestAsync(new HostRequest{Op="command",Viewer=p,Command=intent});Assert.True(state.Success,state.Message);
        }
        foreach(string p in new[]{"Ryan","John"})
        {
            Assert.Equal(2,state.View.Edges.Count(e=>e.Owner==p));Assert.Equal(2,state.View.Vertices.Count(v=>v.Owner==p));
        }
        string active=state.View.ActivePlayer;
        state=await transport.RequestAsync(new HostRequest{Op="command",Viewer=active,Command=new Intent{Kind="ContinueTurn",Player=active}});Assert.True(state.Success);
        state=await transport.RequestAsync(new HostRequest{Op="command",Viewer=active,Command=new Intent{Kind="Roll",Player=active}});Assert.True(state.Success);
        Assert.NotNull(state.View.Dice);Assert.Equal(state.View.LastRoll,state.View.Dice.First+state.View.Dice.Second);Assert.Equal(5,state.View.Dice.ResourcesReceived.Length);
    }
    private static string Repository()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Game.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Cannot locate Game.sln.");
    }
}
