using Game.Contracts;
using Game.Engine;
using Game.LocalHost;
using Xunit;
namespace Game.Engine.Tests;

public sealed class LocalHostTests
{
    private static Request New(string id = "create", ulong seed = 42) => new(id, "create", "Ryan", seed, ["Ryan", "John"]);
    private static Response Send(Session session, string actor, CommandKind kind, int target = -1) => session.Handle(new(Guid.NewGuid().ToString(), "command", actor, Command: new(kind, actor, target)));
    [Fact]
    public void OfflineAdapterReadiesStartsAndCompletesLegalSetupThroughWireSnapshots()
    {
        var session = new Session(new ManualClock()); var create = session.Handle(New()); Assert.True(create.Success); Assert.Equal(Phase.Lobby, create.View!.Phase);
        Assert.True(Send(session, "Ryan", CommandKind.Ready).Success); Assert.True(Send(session, "John", CommandKind.Ready).Success);
        var response = Send(session, "Ryan", CommandKind.Start); Assert.True(response.Success);
        Assert.Equal(19, response.View!.Hexes.Count); Assert.Equal(72, response.View.Edges.Count);
        while (response.View!.Phase is Phase.SetupSettlement or Phase.SetupRoad)
        {
            string actor = response.View.ActivePlayer!;
            response = session.Handle(new("legal", "view", actor));
            response = response.View!.Phase == Phase.SetupSettlement
                ? Send(session, actor, CommandKind.PlaceSettlement, response.LegalSettlements![0])
                : Send(session, actor, CommandKind.PlaceRoad, response.LegalRoads![0]);
            Assert.True(response.Success, response.Message);
        }
        Assert.Equal(Phase.AwaitRoll, response.View!.Phase);
        response = Send(session, response.View.ActivePlayer!, CommandKind.Roll); Assert.True(response.Success); Assert.InRange(response.View!.LastRoll!.Value, 2, 12);
    }
    [Fact]
    public void AdapterSnapshotsHideOtherHandsAndRejectActorMismatchAndUnknownViewers()
    {
        var session = new Session(new ManualClock()); Assert.True(session.Handle(New()).Success);
        var view = session.Handle(new("view", "view", "Ryan"));
        Assert.NotNull(view.View!.Players[0].Resources); Assert.Null(view.View.Players[1].Resources);
        Assert.False(session.Handle(new("bad", "command", "Ryan", Command: new(CommandKind.Ready, "John"))).Success);
        Assert.False(session.Handle(new("unknown", "view", "stranger")).Success);
        var john = session.Handle(new("view-john", "view", "John")); Assert.NotNull(john.View!.Players[1].Resources); Assert.Null(john.View.Players[0].Resources);
    }
    [Fact]
    public void InvalidNewGameCannotReplaceExistingGame()
    {
        var session = new Session(new ManualClock()); var first = session.Handle(New());
        Assert.False(session.Handle(new("bad", "create", "Ryan", Players: ["Ryan", "Ryan"])).Success);
        Assert.Equal(first.View!.GameId, session.Handle(new("view", "view", "Ryan")).View!.GameId);
        Assert.False(session.Handle(new("bad-op", "bogus", "Ryan")).Success);
        Assert.False(session.Handle(new("missing", "command", "Ryan")).Success);
    }
    [Fact]
    public void PollingAdvancesAuthoritativeTimersAndCommandRetriesRemainIdempotent()
    {
        var clock = new ManualClock(); var session = new Session(clock); Assert.True(session.Handle(New()).Success);
        Send(session, "Ryan", CommandKind.Ready); Send(session, "John", CommandKind.Ready);
        var start = Send(session, "Ryan", CommandKind.Start); clock.Advance(240_000); session.Tick();
        var view = session.Handle(new("view", "view", "Ryan")); Assert.Equal(Phase.AwaitRoll, view.View!.Phase);
        string actor = view.View.ActivePlayer!; var intent = new Command(CommandKind.Roll, actor, RequestId: "same");
        var first = session.Handle(new("first", "command", actor, Command: intent));
        var second = session.Handle(new("second", "command", actor, Command: intent));
        Assert.True(first.Success); Assert.True(second.Success); Assert.Equal(first.View!.Version, second.View!.Version); Assert.Equal(first.View.LastRoll, second.View.LastRoll);
        Assert.NotEqual(start.View!.Version, first.View.Version);
    }
}
