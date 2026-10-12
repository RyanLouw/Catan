using Game.Contracts;
using Game.Engine;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class SetupAndRoadRegressionTests
{
    [Fact]
    public void BothInitialRoadsArePlacedAndSecondMustTouchSecondSettlement()
    {
        var e=Lobby(42,2);OK(e,new(CommandKind.Start,"P0"));
        while(e.TestState.Turn==0)
        {
            string p=Active(e);int pi=e.TestState.Active;
            int v=e.LegalSettlements(p,true)[0];OK(e,new(CommandKind.PlaceSettlement,p,v));
            Assert.All(e.LegalRoads(p,true),id=>Assert.True(e.TestState.Board!.Edges[id].A==v||e.TestState.Board.Edges[id].B==v));
            if(e.TestState.SetupStep>=2)
            {
                int other=e.LegalRoads(p).First(id=>!e.LegalRoads(p,true).Contains(id));
                RejectedUnchanged(e,new(CommandKind.PlaceRoad,p,other));
            }
            OK(e,new(CommandKind.PlaceRoad,p,e.LegalRoads(p,true)[0]));
            Assert.InRange(e.TestState.Roads.Values.Count(owner=>owner==pi),1,2);
        }
        foreach(int p in new[]{0,1}){Assert.Equal(2,e.TestState.Roads.Values.Count(owner=>owner==p));Assert.Equal(2,e.TestState.Buildings.Values.Count(b=>b.Owner==p));}
        Assert.NotEmpty(e.View("P0").StartingRolls);Assert.Equal(e.TestState.Players[e.TestState.Starting].Id,e.View("P0").StartingPlayer);
    }
    [Fact]
    public void NormalRoadCanExtendFromEitherStartingSettlementAndAnyUnblockedOwnedRoad()
    {
        var e=Started(42,2);Action(e);string p=Active(e);int pi=e.TestState.Active;Resources(e,pi,10,10,0,0,0);
        foreach(int vertex in e.TestState.Buildings.Where(kv=>kv.Value.Owner==pi).Select(kv=>kv.Key).ToArray())
        {
            int road=e.LegalRoads(p).First(id=>e.TestState.Board!.Edges[id].A==vertex||e.TestState.Board.Edges[id].B==vertex);
            OK(e,new(CommandKind.BuildRoad,p,road));
        }
        var owned=e.TestState.Roads.Where(kv=>kv.Value==pi).Select(kv=>kv.Key).ToArray();
        int continuation=e.LegalRoads(p).First(id=>new[]{e.TestState.Board!.Edges[id].A,e.TestState.Board.Edges[id].B}.Any(v=>!e.TestState.Buildings.ContainsKey(v)&&e.TestState.Board.VertexEdges[v].Any(owned.Contains)));
        OK(e,new(CommandKind.BuildRoad,p,continuation));
    }
    [Fact]
    public void ClassicDistanceRuleBlocksBothFriendlyAndEnemyNeighbours()
    {
        var e=Started(42,2);Action(e);string p=Active(e);int pi=e.TestState.Active;Resources(e,pi,2,2,2,2,0);
        foreach(int owner in new[]{pi,(pi+1)%2})
        {
            int v=e.TestState.Buildings.First(kv=>kv.Value.Owner==owner).Key;
            foreach(int neighbour in e.TestState.Board!.Neighbors(v))RejectedUnchanged(e,new(CommandKind.BuildSettlement,p,neighbour));
        }
    }
}
