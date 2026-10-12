using Game.Contracts;
using Game.Engine;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class TradeAndProductionTests
{
    [Fact]
    public void PlayerTradeIsAtomicAndRechecksBothBalances()
    {
        var e = Started(); Action(e, 0); Resources(e, 0, 2, 0, 0, 0, 0); Resources(e, 1, 0, 2, 0, 0, 0);
        OK(e, new(CommandKind.OfferTrade, "P0", Give: [2, 0, 0, 0, 0], Take: [0, 2, 0, 0, 0], OtherPlayer: "P1"));
        int id = e.TestState.Trades[0].Id; RejectedUnchanged(e, new(CommandKind.AcceptTrade, "P2", id));
        Resources(e, 1, 0, 1, 0, 0, 0); RejectedUnchanged(e, new(CommandKind.AcceptTrade, "P1", id));
        Resources(e, 1, 0, 2, 0, 0, 0); OK(e, new(CommandKind.AcceptTrade, "P1", id));
        Assert.Equal(new[] { 0, 2, 0, 0, 0 }, e.TestState.Players[0].Resources);
        Assert.Equal(new[] { 2, 0, 0, 0, 0 }, e.TestState.Players[1].Resources); Assert.Empty(e.TestState.Trades);
        RejectedUnchanged(e, new(CommandKind.AcceptTrade, "P1", id));
    }
    [Fact]
    public void InvalidTradesAndBundlesAreRejected()
    {
        var e = Started(); Action(e, 0); Resources(e, 0, 5, 5, 5, 5, 0);
        foreach (int[] bundle in new[] { Array.Empty<int>(), new[] { -1, 0, 0, 0, 0 }, new[] { int.MaxValue, 0, 0, 0, 0 } })
            RejectedUnchanged(e, new(CommandKind.OfferTrade, "P0", Give: bundle, Take: [0, 1, 0, 0, 0], OtherPlayer: "P1"));
        RejectedUnchanged(e, new(CommandKind.OfferTrade, "P0", Give: [1, 0, 0, 0, 0], Take: [1, 0, 0, 0, 0], OtherPlayer: "P1"));
        RejectedUnchanged(e, new(CommandKind.OfferTrade, "P0", Give: [1, 0, 0, 0, 0], Take: [0, 1, 0, 0, 0], OtherPlayer: "P0"));
        RejectedUnchanged(e, new(CommandKind.BankTrade, "P0", Give: [1, 0, 0, 0, 0], Take: [0, 1, 0, 0, 0]));
    }
    [Theory]
    [InlineData(0, 4)] [InlineData(1, 3)] [InlineData(2, 2)]
    public void EngineDeterminesBankAndHarborRatios(int mode, int ratio)
    {
        var e = Started(); Action(e, 0); e.TestState.Buildings.Clear();
        if (mode > 0)
        {
            var port = e.TestState.Board!.Ports.First(p => mode == 1 ? p.Resource == null : p.Resource == Resource.Brick);
            e.TestState.Buildings.Add(port.A, new(0, 1));
        }
        Assert.Equal(ratio, e.TradeRatio("P0", Resource.Brick)); Resources(e, 0, ratio * 2, 0, 0, 0, 0);
        OK(e, new(CommandKind.BankTrade, "P0", Give: [ratio * 2, 0, 0, 0, 0], Take: [0, 2, 0, 0, 0]));
        Assert.Equal(new[] { 0, 2, 0, 0, 0 }, e.TestState.Players[0].Resources);
        e.Validate();
    }
    [Fact]
    public void EmptyBankTradeCannotSpendResources()
    {
        var e = Started(); Action(e, 0); e.TestState.Buildings.Clear();
        for (int p = 0; p < 4; p++) Resources(e, p, 0, 0, 0, 0, 0);
        Resources(e, 0, 4, 0, 0, 0, 0); Resources(e, 1, 0, 19, 0, 0, 0);
        RejectedUnchanged(e, new(CommandKind.BankTrade, "P0", Give: [4, 0, 0, 0, 0], Take: [0, 1, 0, 0, 0]));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ProductionHonorsRobberAndBuildingLevel(bool blocked)
    {
        var e = ProductionFixture(); var s = e.TestState; s.Buildings[0] = new(0, 2); s.Robber = blocked ? 0 : 1;
        GameEngine.Produce(s, 6); Assert.Equal(blocked ? 0 : 2, s.Players[0].Resources[0]); e.Validate();
    }
    [Fact]
    public void MultipleClaimantsReceiveNothingWhenBankShortButSoleClaimantGetsRemainingSupply()
    {
        var e = ProductionFixture(); var s = e.TestState; s.Buildings[0] = new(0, 2); s.Buildings[2] = new(1, 1);
        Resources(e, 2, 18, 0, 0, 0, 0); GameEngine.Produce(s, 6);
        Assert.Equal(0, s.Players[0].Resources[0]); Assert.Equal(0, s.Players[1].Resources[0]); Assert.Equal(1, s.Bank[0]);
        s.Buildings.Remove(2); GameEngine.Produce(s, 6); Assert.Equal(1, s.Players[0].Resources[0]); Assert.Equal(0, s.Bank[0]); e.Validate();
    }
    [Fact]
    public void TheSameRollCanProduceResourcesForEveryPlayerNotOnlyTheRoller()
    {
        for(ulong seed=1;seed<=100;seed++)
        {
            var e=Started(seed);var s=e.TestState;s.Buildings.Clear();s.Roads.Clear();
            for(int p=0;p<4;p++)Resources(e,p,0,0,0,0,0);
            var candidates=s.Board!.Hexes.Where(h=>h.Number==6).SelectMany(h=>h.Vertices).Distinct().ToArray();
            for(int p=0;p<4;p++)
            {
                int v=candidates.First(v=>!s.Buildings.ContainsKey(v)&&!s.Board.Neighbors(v).Any(s.Buildings.ContainsKey));s.Buildings[v]=new(p,1);
            }
            s.Robber=s.Board.Hexes.First(h=>h.Resource==null).Id;
            OK(e,new(CommandKind.Roll,Active(e)));
            if(e.TestState.Roll!=6)continue;
            Assert.All(e.TestState.Players,p=>Assert.True(p.Resources.Sum()>0));e.Validate();return;
        }
        throw new InvalidOperationException("No seed rolling six found.");
    }
    private static GameEngine ProductionFixture()
    {
        var e = Lobby(); var edges = new[] { new Edge(0, 0, 1), new Edge(1, 1, 2), new Edge(2, 2, 3), new Edge(3, 3, 4), new Edge(4, 4, 5), new Edge(5, 5, 0) };
        e.TestState.Board = new Board { Hexes = [new(0, 0, 0, Resource.Brick, 6, [0, 1, 2, 3, 4, 5]), new(1, 1, 0, null, 0, [])],
            Edges = edges, Ports = [], VertexEdges = Enumerable.Range(0, 6).Select(v => edges.Where(edge => edge.A == v || edge.B == v).Select(edge => edge.Id).ToArray()).ToArray() };
        e.TestState.Robber = 1; return e;
    }
}
