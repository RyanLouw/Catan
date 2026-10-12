using Game.Contracts;
using Game.Engine;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class TimerAndRobberTests
{
    [Fact]
    public void ConnectedRollAutomaticallyOccursAt15Seconds()
    {
        var clock = new ManualClock(); var e = Started(clock: clock); clock.Advance(14_999); e.Tick();
        Assert.Equal(Phase.AwaitRoll, e.TestState.Phase); Assert.Null(e.TestState.Roll);
        clock.Advance(1); e.Tick(); Assert.InRange(e.TestState.Roll!.Value, 2, 12); Assert.NotEqual(Phase.AwaitRoll, e.TestState.Phase);
        RejectedUnchanged(e, new(CommandKind.Roll, Active(e)));
    }
    [Fact]
    public void ActionDeadlineIs90SecondsAndTradeDoesNotExtendIt()
    {
        var clock = new ManualClock(); var e = Started(clock: clock); Action(e, 0); Resources(e, 0, 1, 0, 0, 0, 0);
        clock.Advance(30_000); OK(e, new(CommandKind.OfferTrade, "P0", Give: [1, 0, 0, 0, 0], Take: [0, 1, 0, 0, 0], OtherPlayer: "P1"));
        Assert.Equal(90_000, e.TestState.Deadline); clock.Advance(59_999); e.Tick(); Assert.Equal(0, e.TestState.Active);
        clock.Advance(1); e.Tick(); Assert.Equal(1, e.TestState.Active); Assert.Empty(e.TestState.Trades); Assert.Equal(Phase.AwaitRoll, e.TestState.Phase);
    }
    [Fact]
    public void LateCommandsCannotBeatTheHostTimer()
    {
        var clock = new ManualClock(); var e = Started(clock: clock); Action(e, 0); Resources(e, 0, 1, 1, 0, 0, 0);
        int edge = e.LegalRoads("P0")[0]; clock.Advance(90_000);
        var result = e.Execute(new(CommandKind.BuildRoad, "P0", edge)); Assert.False(result.Success);
        Assert.Equal(1, e.TestState.Active); Assert.False(e.TestState.Roads.ContainsKey(edge));
    }
    [Fact]
    public void DisconnectedTurnHas10SecondGraceThenSkipsEntireTurn()
    {
        var clock = new ManualClock(); var e = Started(clock: clock); Action(e, 0);
        OK(e, new(CommandKind.Disconnect, "P1")); OK(e, new(CommandKind.EndTurn, "P0"));
        Assert.Equal(Phase.ReconnectGrace, e.TestState.Phase); Assert.Equal(10_000, e.TestState.Deadline);
        clock.Advance(9_999); e.Tick(); Assert.Equal(1, e.TestState.Active);
        clock.Advance(1); e.Tick(); Assert.Equal(2, e.TestState.Active); Assert.Equal(Phase.AwaitRoll, e.TestState.Phase); Assert.Null(e.TestState.Roll);
    }
    [Fact]
    public void ReconnectRestoresTurnAndPrivateSnapshot()
    {
        var clock = new ManualClock(); var e = Started(clock: clock); Action(e, 0); GrantCard(e, 1, Card.VictoryPoint);
        OK(e, new(CommandKind.Disconnect, "P1")); OK(e, new(CommandKind.EndTurn, "P0")); clock.Advance(9_000);
        OK(e, new(CommandKind.Reconnect, "P1")); Assert.Equal(Phase.AwaitRoll, e.TestState.Phase); Assert.Equal(24_000, e.TestState.Deadline);
        Assert.Contains(Card.VictoryPoint, e.View("P1").Players[1].Cards!); Assert.Null(e.View("P0").Players[1].Cards);
    }
    [Fact]
    public void SurrenderKeepsPiecesAndSkipsWithoutGrace()
    {
        var e = Started(); Action(e, 0); int[] vertices = e.TestState.Buildings.Where(kv => kv.Value.Owner == 1).Select(kv => kv.Key).ToArray();
        int[] roads = e.TestState.Roads.Where(kv => kv.Value == 1).Select(kv => kv.Key).ToArray();
        OK(e, new(CommandKind.Surrender, "P1")); OK(e, new(CommandKind.EndTurn, "P0")); Assert.Equal(2, e.TestState.Active);
        Assert.All(vertices, v => Assert.Equal(1, e.TestState.Buildings[v].Owner)); Assert.All(roads, r => Assert.Equal(1, e.TestState.Roads[r]));
        RejectedUnchanged(e, new(CommandKind.Reconnect, "P1"));
    }
    [Fact]
    public void SetupTimeoutCompletesLegallyAndReproducibly()
    {
        var ca = new ManualClock(); var cb = new ManualClock(); var a = Lobby(111, clock: ca); var b = Lobby(111, clock: cb);
        OK(a, new(CommandKind.Start, "P0")); OK(b, new(CommandKind.Start, "P0")); ca.Advance(480_000); cb.Advance(480_000); a.Tick(); b.Tick();
        Assert.Equal(Phase.AwaitRoll, a.TestState.Phase); Assert.Equal(8, a.TestState.Buildings.Count); Assert.Equal(a.TestState.Buildings, b.TestState.Buildings); Assert.Equal(a.TestState.Roads, b.TestState.Roads);
        a.Validate(); b.Validate();
    }
    [Fact]
    public void RollSevenExplicitlyWaitsForDiscardsThenRobberAndVictimBeforeActionTimer()
    {
        var clock = new ManualClock(); var e = Seven(clock); string active = Active(e);
        Assert.Equal(Phase.Discard, e.TestState.Phase); Assert.Null(e.TestState.ActionDeadline); Assert.Equal(30_000, e.TestState.Deadline);
        string discarder = e.TestState.Players.First(p => p.Resources.Sum() > 7).Id;
        RejectedUnchanged(e, new(CommandKind.Discard, discarder, Give: [1, 0, 0, 0, 0]));
        RejectedUnchanged(e, new(CommandKind.BuildRoad, active, e.LegalRoads(active)[0]));
        clock.Advance(30_000); e.Tick(); Assert.Equal(Phase.Robber, e.TestState.Phase); Assert.Null(e.TestState.ActionDeadline);
        Assert.All(e.TestState.Players, p => Assert.True(p.Resources.Sum() <= 7));
        RejectedUnchanged(e, new(CommandKind.MoveRobber, active, e.TestState.Robber));
        int target = e.TestState.Board!.Hexes.First(h => h.Id != e.TestState.Robber && h.Vertices.All(v => !e.TestState.Buildings.ContainsKey(v))).Id;
        OK(e, new(CommandKind.MoveRobber, active, target)); Assert.Equal(Phase.Action, e.TestState.Phase); Assert.Equal(120_000, e.TestState.ActionDeadline);
    }
    [Fact]
    public void MandatoryTimeoutsCannotLeaveSevenSequenceBlocked()
    {
        var clock = new ManualClock(); var e = Seven(clock); clock.Advance(90_000); e.Tick();
        Assert.Equal(Phase.Action, e.TestState.Phase); Assert.InRange(e.TestState.ActionDeadline!.Value, 150_000, 180_000); e.Validate();
    }
    [Fact]
    public void MultipleVictimsRequireChoiceAndStealOneResourceDeterministically()
    {
        var a = VictimFixture(); var b = VictimFixture(); int total = a.TestState.Players[0].Resources.Sum();
        OK(a, new(CommandKind.MoveRobber, "P0", 0)); OK(b, new(CommandKind.MoveRobber, "P0", 0));
        Assert.Equal(Phase.Victim, a.TestState.Phase); RejectedUnchanged(a, new(CommandKind.ChooseVictim, "P0", OtherPlayer: "P3"));
        OK(a, new(CommandKind.ChooseVictim, "P0", OtherPlayer: "P1")); OK(b, new(CommandKind.ChooseVictim, "P0", OtherPlayer: "P1"));
        Assert.Equal(total + 1, a.TestState.Players[0].Resources.Sum()); Assert.Equal(a.TestState.Players[0].Resources, b.TestState.Players[0].Resources); Assert.Equal(Phase.Action, a.TestState.Phase);
    }
    [Fact]
    public void SurrenderDuringOtherPlayersDiscardsFinishesMandatoryEffectsAndSkipsImmediately()
    {
        var clock = new ManualClock(); var e = Seven(clock); string active = Active(e); int oldTurn = e.TestState.Turn;
        OK(e, new(CommandKind.Surrender, active)); Assert.Equal(Phase.Discard, e.TestState.Phase);
        clock.Advance(30_000); e.Tick(); Assert.True(e.TestState.Turn > oldTurn); Assert.Equal(Phase.AwaitRoll, e.TestState.Phase);
    }
    [Fact]
    public void PreRollKnightDoesNotExtendOriginalRollDeadline()
    {
        var clock = new ManualClock(); var e = Started(clock: clock); GrantCard(e, e.TestState.Active, Card.Knight); string p = Active(e);
        clock.Advance(14_000); OK(e, new(CommandKind.PlayKnight, p));
        clock.Advance(1_000); int target = e.TestState.Board!.Hexes.First(h => h.Id != e.TestState.Robber && h.Vertices.All(v => !e.TestState.Buildings.ContainsKey(v))).Id;
        OK(e, new(CommandKind.MoveRobber, p, target)); e.Tick(); Assert.NotNull(e.TestState.Roll);
    }
    private static GameEngine Seven(ManualClock clock)
    {
        for (ulong seed = 1; seed < 1000; seed++)
        {
            var e = Started(seed, clock: clock); Resources(e, 0, 9, 0, 0, 0, 0); Resources(e, 1, 0, 9, 0, 0, 0);
            OK(e, new(CommandKind.Roll, Active(e))); if (e.TestState.Roll == 7) return e;
        }
        throw new InvalidOperationException("No seven seed found.");
    }
    private static GameEngine VictimFixture()
    {
        var e = Started(19); e.TestState.Active = 0; e.TestState.Buildings.Clear(); var h = e.TestState.Board!.Hexes[0];
        e.TestState.Buildings[h.Vertices[0]] = new(1, 1); e.TestState.Buildings[h.Vertices[2]] = new(2, 1);
        Resources(e, 1, 1, 2, 1, 0, 0); Resources(e, 2, 1, 0, 0, 0, 0);
        e.TestState.Robber = 1; e.TestState.Phase = Phase.Robber; e.TestState.RobberReturn = Phase.Action; return e;
    }
}
