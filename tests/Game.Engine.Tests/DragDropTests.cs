using Game.Contracts;
using Game.Engine;
using Catan.Client;
using Game.LocalHost;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class DragDropTests
{
    private static HostResponse UI(string phase="Action")=>new(){LegalRoads=[7],LegalSettlements=[8],LegalCities=[9],View=new Snapshot{Phase=phase,ActivePlayer="Ryan",RobberHex=0,Hexes=[new Terrain{Id=0},new Terrain{Id=1}]}};
    [Theory]
    [InlineData("SetupSettlement","Settlement",8,"PlaceSettlement")]
    [InlineData("SetupRoad","Road",7,"PlaceRoad")]
    [InlineData("Action","Settlement",8,"BuildSettlement")]
    [InlineData("Action","Road",7,"BuildRoad")]
    [InlineData("Action","City",9,"BuildCity")]
    [InlineData("Robber","Robber",1,"MoveRobber")]
    public void GesturesMapToCorrectIntent(string phase,string piece,int target,string kind)
    {
        var intent=ClientActions.Drop(UI(phase),"Ryan",piece,target);Assert.NotNull(intent);Assert.Equal(kind,intent.Kind);Assert.Equal(target,intent.Target);Assert.Equal("Ryan",intent.Player);Assert.NotNull(intent.RequestId);
    }
    [Theory]
    [InlineData("AwaitRoll","Road",7)] [InlineData("Action","Road",80)] [InlineData("Action","City",8)] [InlineData("Robber","Robber",0)] [InlineData("SetupRoad","Settlement",8)]
    public void WrongPhasesInvalidTargetsAndWrongActorsNeverCreateCommands(string phase,string piece,int target)
    {
        Assert.Null(ClientActions.Drop(UI(phase),"Ryan",piece,target));Assert.Null(ClientActions.Drop(UI(),"John",piece,target));
    }
    [Fact]
    public void TimerUsesHostDeadlineIncludingActionExpiryDuringKnightAndClampsAtZero()
    {
        var ui=UI("Robber");ui.ServerMilliseconds=1000;ui.View.DeadlineMilliseconds=31000;ui.View.ActionDeadlineMilliseconds=11000;
        Assert.Equal(7,ClientActions.Remaining(ui,3));Assert.Equal(0,ClientActions.Remaining(ui,12));Assert.Equal(10,ClientActions.Remaining(ui,-1));
    }
    [Fact]
    public void FreeRoadPreviewDoesNotSpendCardResourcesOrAdvanceVersionAndSecondRoadCanConnectToFirst()
    {
        var e=Started();Action(e);string p=Active(e);GrantCard(e,e.TestState.Active,Game.Contracts.Card.RoadBuilding);
        int first=e.LegalRoads(p)[0];string before=Snapshot(e);var preview=e.PreviewRoadBuilding(p,first);
        Assert.True(preview.Valid);Assert.DoesNotContain(first,preview.SecondRoads);Assert.Equal(before,Snapshot(e));
        Assert.False(e.PreviewRoadBuilding(p,1000).Valid);Assert.Equal(before,Snapshot(e));
        int second=preview.SecondRoads[0];OK(e,new(CommandKind.PlayRoadBuilding,p,first,second));Assert.Equal(e.TestState.Active,e.TestState.Roads[first]);Assert.Equal(e.TestState.Active,e.TestState.Roads[second]);
    }
    [Fact]
    public void PreviewRejectsUnavailableNewlyBoughtAndAlreadyPlayedCards()
    {
        var e=Started();Action(e);string p=Active(e);int first=e.LegalRoads(p)[0];Assert.False(e.PreviewRoadBuilding(p,first).Valid);
        GrantCard(e,e.TestState.Active,Game.Contracts.Card.RoadBuilding,true);Assert.False(e.PreviewRoadBuilding(p,first).Valid);
        e.TestState.Players[e.TestState.Active].Cards[0]=new(Game.Contracts.Card.RoadBuilding,e.TestState.Turn-1);e.TestState.PlayedCard=true;Assert.False(e.PreviewRoadBuilding(p,first).Valid);
    }
    [Fact]
    public void HostSendsAuthoritativeCostsAndCityTargets()
    {
        var session=new Session(new ManualClock());var v=session.Handle(new("create","create","Ryan",Players:["Ryan","John"]));
        Assert.Equal(Game.Engine.Costs.Road,v.Costs!["Road"]);Assert.Equal(Game.Engine.Costs.Settlement,v.Costs["Settlement"]);Assert.Equal(Game.Engine.Costs.City,v.Costs["City"]);Assert.Empty(v.LegalCities!);
    }
}
