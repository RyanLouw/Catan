using Game.Contracts;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;
public sealed class OpenTradeTests
{
    [Fact]
    public void OpenOfferCanBeAcceptedByAnyOpponentOnlyOnceAndNeverByItsCreator()
    {
        var e=Started();Action(e,0);Resources(e,0,2,0,0,0,0);Resources(e,1,0,2,0,0,0);Resources(e,2,0,2,0,0,0);
        OK(e,new(CommandKind.OfferTrade,"P0",Give:[2,0,0,0,0],Take:[0,2,0,0,0]));var trade=e.View("P2").Trades.Single();Assert.Null(trade.To);
        RejectedUnchanged(e,new(CommandKind.AcceptTrade,"P0",trade.Id));OK(e,new(CommandKind.AcceptTrade,"P2",trade.Id));
        Assert.Equal(new[]{0,2,0,0,0},e.TestState.Players[0].Resources);Assert.Equal(new[]{2,0,0,0,0},e.TestState.Players[2].Resources);RejectedUnchanged(e,new(CommandKind.AcceptTrade,"P1",trade.Id));
    }
    [Fact]
    public void OpenOfferRechecksBalancesAndOtherPlayersCannotCancelIt()
    {
        var e=Started();Action(e,0);Resources(e,0,2,0,0,0,0);Resources(e,1,0,1,0,0,0);
        OK(e,new(CommandKind.OfferTrade,"P0",Give:[2,0,0,0,0],Take:[0,2,0,0,0]));int id=e.TestState.Trades.Single().Id;
        RejectedUnchanged(e,new(CommandKind.AcceptTrade,"P1",id));RejectedUnchanged(e,new(CommandKind.CancelTrade,"P1",id));OK(e,new(CommandKind.CancelTrade,"P0",id));Assert.Empty(e.TestState.Trades);
    }
    [Fact]
    public void OpenOffersEndWithTheActiveTurnAndAreRejectedFromOtherTurns()
    {
        var e=Started();Action(e,0);Resources(e,0,1,0,0,0,0);
        RejectedUnchanged(e,new(CommandKind.OfferTrade,"P1",Give:[1,0,0,0,0],Take:[0,1,0,0,0]));
        OK(e,new(CommandKind.OfferTrade,"P0",Give:[1,0,0,0,0],Take:[0,1,0,0,0]));OK(e,new(CommandKind.EndTurn,"P0"));Assert.Empty(e.TestState.Trades);
    }
}
