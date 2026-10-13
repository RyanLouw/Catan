using Catan.Client;
using Game.Contracts;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;
public sealed class AffordabilityTests
{
    private static HostResponse UI()=>new(){Costs=new(){{"Road",[1,1,0,0,0]},{"Settlement",[1,1,1,1,0]},{"City",[0,0,0,2,3]}},LegalRoads=[7],LegalSettlements=[8],LegalCities=[9],View=new Snapshot{Phase="Action",ActivePlayer="A",Players=[new Participant{Id="A",Resources=[0,1,0,1,1]},new Participant{Id="B",Resources=[1,0,0,0,0]}]}};
    [Theory]
    [InlineData("Road",7)] [InlineData("Settlement",8)] [InlineData("City",9)]
    public void UnaffordableLegalDropsAreBlockedUntilRequiredCardsAreAvailable(string piece,int target)
    {
        var ui=UI();Assert.False(ClientActions.CanAfford(ui,"A",piece));Assert.Null(ClientActions.Drop(ui,"A",piece,target));
        Assert.Equal(ui.Costs[piece].Select((n,i)=>Math.Max(0,n-ui.View.Players[0].Resources[i])).ToArray(),ClientActions.Missing(ui,"A",piece));
        ui.View.Players[0].Resources=ui.Costs[piece].ToArray();Assert.True(ClientActions.CanAfford(ui,"A",piece));Assert.NotNull(ClientActions.Drop(ui,"A",piece,target));
    }
    [Fact]
    public void InitialPlacementsRemainFreeAndUnknownCostsCannotAuthorizePaidBuilds()
    {
        var ui=UI();ui.Costs=null;Assert.Null(ClientActions.Drop(ui,"A","Road",7));ui.View.Phase="SetupRoad";Assert.NotNull(ClientActions.Drop(ui,"A","Road",7));
        ui.View.Phase="SetupSettlement";Assert.NotNull(ClientActions.Drop(ui,"A","Settlement",8));
    }
    [Fact]
    public void RecipientCanAcceptAffordableOpenAndNamedOffersWithoutOwningTheTurn()
    {
        var ui=UI();var offer=new Offer{Id=1,From="A",To="B",Give=[0,1,0,0,0],Take=[1,0,0,0,0]};
        Assert.True(ClientActions.CanAccept(ui,"B",offer));Assert.False(ClientActions.CanAccept(ui,"A",offer));
        offer.To=null;Assert.True(ClientActions.CanAccept(ui,"B",offer));ui.View.Players[1].Resources[0]=0;Assert.False(ClientActions.CanAccept(ui,"B",offer));
        ui.View.Phase="Handover";Assert.False(ClientActions.CanAccept(ui,"B",offer));
    }
    [Fact]
    public void TradeRecipientReviewCanSwitchHandsAndAcceptWhileOfferingPlayersTurnStaysActive()
    {
        var e=Started();Action(e,0);Resources(e,0,0,2,0,0,0);Resources(e,1,1,0,0,0,0);
        OK(e,new(CommandKind.OfferTrade,"P0",Give:[0,2,0,0,0],Take:[1,0,0,0,0],OtherPlayer:"P1"));
        var view=e.View("P1");Assert.Equal("P0",view.ActivePlayer);Assert.NotNull(view.Players[1].Resources);
        OK(e,new(CommandKind.AcceptTrade,"P1",view.Trades.Single().Id));Assert.Equal(new[]{0,2,0,0,0},e.TestState.Players[1].Resources);
        Assert.Equal(new[]{1,0,0,0,0},e.TestState.Players[0].Resources);Assert.Equal("P0",e.View("P1").ActivePlayer);
    }
}
