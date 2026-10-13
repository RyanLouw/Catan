using Game.Contracts;
using Game.Engine;
using Game.LocalHost;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;
public sealed class TestingControlsTests
{
    [Fact]
    public void ChosenDiceRequireExplicitTestingAndValidateBothFaces()
    {
        var normal=Started();RejectedUnchanged(normal,new(CommandKind.Roll,Active(normal),FirstDie:3,SecondDie:3));
        var e=new GameEngine(1,new ManualClock(),testing:true){TestState=Started().TestState.Clone()};
        RejectedUnchanged(e,new(CommandKind.Roll,Active(e),FirstDie:0,SecondDie:3));
        RejectedUnchanged(e,new(CommandKind.Roll,Active(e),FirstDie:3));
        OK(e,new(CommandKind.Roll,Active(e),FirstDie:3,SecondDie:3,RequestId:"dice"));
        Assert.Equal(6,e.View(Active(e)).Dice!.Total);Assert.Equal(3,e.View(Active(e)).Dice!.First);
        var before=Snapshot(e);OK(e,new(CommandKind.Roll,Active(e),FirstDie:3,SecondDie:3,RequestId:"dice"));Assert.Equal(before,Snapshot(e));
    }
    [Fact]
    public void ChosenRollProducesExactResourcesForEveryEligibleBuilding()
    {
        var e=new GameEngine(1,new ManualClock(),testing:true){TestState=Started().TestState.Clone()};
        var s=e.TestState;int total=s.Board!.Hexes.First(h=>h.Id!=s.Robber&&h.Resource.HasValue&&h.Vertices.Any(v=>s.Buildings.ContainsKey(v))).Number;var expected=new int[s.Players.Count,5];
        foreach(var h in s.Board!.Hexes.Where(h=>h.Number==total&&h.Id!=s.Robber&&h.Resource.HasValue))
            foreach(int v in h.Vertices)if(s.Buildings.TryGetValue(v,out var b))expected[b.Owner,(int)h.Resource!.Value]+=b.Level;
        var before=s.Players.Select(p=>p.Resources.ToArray()).ToArray();
        OK(e,new(CommandKind.Roll,Active(e),FirstDie:Math.Min(6,total-1),SecondDie:total-Math.Min(6,total-1)));
        for(int p=0;p<s.Players.Count;p++)for(int r=0;r<5;r++)Assert.Equal(before[p][r]+expected[p,r],e.TestState.Players[p].Resources[r]);
    }
    [Fact]
    public void ChosenSevenUsesRobberRulesWithoutProduction()
    {
        var e=new GameEngine(1,new ManualClock(),testing:true){TestState=Started().TestState.Clone()};
        var before=e.TestState.Players.Select(p=>p.Resources.ToArray()).ToArray();
        OK(e,new(CommandKind.Roll,Active(e),FirstDie:3,SecondDie:4));Assert.Equal(Phase.Robber,e.TestState.Phase);
        for(int p=0;p<before.Length;p++)Assert.Equal(before[p],e.TestState.Players[p].Resources);
    }
    [Fact]
    public void LocalTestingTableExposesResourcesOnlyWhenEnabledAndNeverPrivateCards()
    {
        var session=new Session(new ManualClock());
        var normal=session.Handle(new("normal","create","A",Players:["A","B"]));Assert.Null(normal.TableResources);
        var test=session.Handle(new("test","create","A",Players:["A","B"],Testing:true));
        Assert.True(test.Testing);Assert.Equal(2,test.TableResources!.Count);Assert.Equal(new int[5],test.TableResources["B"]);
        Assert.Null(test.View!.Players[1].Resources);Assert.Null(test.View.Players[1].Cards);
        test.TableResources["B"][0]=99;Assert.Equal(0,session.Handle(new("view","view","A")).TableResources!["B"][0]);
        session.Handle(new("normal-again","create","A",Players:["A","B"]));Assert.Null(session.Handle(new("view2","view","A")).TableResources);
    }
}
