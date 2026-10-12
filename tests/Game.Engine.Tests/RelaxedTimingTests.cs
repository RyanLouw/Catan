using Game.Contracts;
using Game.Engine;
using Game.LocalHost;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class RelaxedTimingTests
{
    private static GameEngine Setup(ManualClock clock)
    {
        var e=new GameEngine(42,clock,GameTiming.RelaxedTesting);
        foreach(string p in new[]{"Ryan","John"}){OK(e,new(CommandKind.AddPlayer,p));OK(e,new(CommandKind.Ready,p));}
        OK(e,new(CommandKind.Start,"Ryan"));return e;
    }
    [Fact]
    public void SetupHandoverDoesNotConsumeNextPlacementTimerAndCanBeAcknowledgedEarly()
    {
        var c=new ManualClock();var e=Setup(c);Assert.Equal(60_000,e.TestState.Deadline);string p=Active(e);
        OK(e,new(CommandKind.PlaceSettlement,p,e.LegalSettlements(p,true)[0]));OK(e,new(CommandKind.PlaceRoad,p,e.LegalRoads(p,true)[0]));
        Assert.Equal(Phase.Handover,e.TestState.Phase);Assert.Equal(Phase.SetupSettlement,e.TestState.NextPhase);Assert.Equal(30_000,e.TestState.Deadline);
        RejectedUnchanged(e,new(CommandKind.ContinueTurn,p));string next=Active(e);c.Advance(10_000);OK(e,new(CommandKind.ContinueTurn,next));
        Assert.Equal(Phase.SetupSettlement,e.TestState.Phase);Assert.Equal(70_000,e.TestState.Deadline);Assert.Null(e.TestState.NextPhase);
    }
    [Fact]
    public void AutomaticHandoverExpiryStartsFreshFullTimer()
    {
        var c=new ManualClock();var e=Setup(c);string p=Active(e);
        OK(e,new(CommandKind.PlaceSettlement,p,e.LegalSettlements(p,true)[0]));OK(e,new(CommandKind.PlaceRoad,p,e.LegalRoads(p,true)[0]));
        c.Advance(29_999);e.Tick();Assert.Equal(Phase.Handover,e.TestState.Phase);c.Advance(1);e.Tick();
        Assert.Equal(Phase.SetupSettlement,e.TestState.Phase);Assert.Equal(90_000,e.TestState.Deadline);
    }
    [Fact]
    public void NormalTurnUses30SecondHandover60SecondRollAnd180SecondActions()
    {
        var c=new ManualClock();var e=Setup(c);
        while(e.TestState.Turn==0)
        {
            string p=Active(e);
            if(e.TestState.Phase==Phase.Handover)OK(e,new(CommandKind.ContinueTurn,p));
            else if(e.TestState.Phase==Phase.SetupSettlement)OK(e,new(CommandKind.PlaceSettlement,p,e.LegalSettlements(p,true)[0]));
            else OK(e,new(CommandKind.PlaceRoad,p,e.LegalRoads(p,true)[0]));
        }
        Assert.Equal(Phase.Handover,e.TestState.Phase);Assert.Null(e.TestState.RollDeadline);string actor=Active(e);OK(e,new(CommandKind.ContinueTurn,actor));
        Assert.Equal(60_000,e.TestState.RollDeadline);c.Advance(59_999);e.Tick();Assert.Null(e.TestState.Roll);c.Advance(1);e.Tick();Assert.NotNull(e.TestState.Roll);
        while(e.TestState.Phase is Phase.Discard or Phase.Robber or Phase.Victim){c.Advance(60_000);e.Tick();}
        Assert.Equal(Phase.Action,e.TestState.Phase);Assert.Equal(c.Milliseconds+180_000,e.TestState.ActionDeadline);
        OK(e,new(CommandKind.EndTurn,actor));Assert.Equal(Phase.Handover,e.TestState.Phase);Assert.Equal(c.Milliseconds+30_000,e.TestState.Deadline);Assert.Null(e.TestState.ActionDeadline);
    }
    [Fact]
    public void HostProfileIsOptInAndStandardEngineRemainsUnchanged()
    {
        var session=new Session(new ManualClock());var standard=session.Handle(new("normal","create","Ryan",Players:["Ryan","John"]));Assert.Equal(GameTiming.Standard,standard.Timing);
        var slow=session.Handle(new("slow","create","Ryan",Players:["Ryan","John"],RelaxedTimers:true));Assert.Equal(GameTiming.RelaxedTesting,slow.Timing);
        Assert.Throws<ArgumentOutOfRangeException>(()=>new GameEngine(1,timing:new GameTiming(RollMilliseconds:0)));
    }
}
