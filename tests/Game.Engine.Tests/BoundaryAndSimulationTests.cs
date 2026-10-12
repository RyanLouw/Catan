using Game.Contracts;
using Game.Engine;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class BoundaryAndSimulationTests
{
    [Theory]
    [InlineData(2, 1)] [InlineData(3, 2)] [InlineData(4, 3)]
    public void CompleteGamesFinishAndAreReproducible(int count, int seed)
    {
        var a = Simulation.Run((ulong)seed, count); var b = Simulation.Run((ulong)seed, count);
        Assert.NotNull(a.Winner); Assert.Equal(a, b); Assert.True(a.Turns > 0); Assert.True(a.Commands > 0);
    }
    [Fact]
    public void HundredsOfMalformedCommandsCannotCorruptState()
    {
        var e = Started(); Action(e); var rng = new Random(1984);
        for (int n = 0; n < 500; n++)
        {
            string before = Snapshot(e);
            var c = new Command((CommandKind)rng.Next(3, 29), Active(e), rng.Next(-10, 100), rng.Next(-10, 100),
                Resource: (Resource)rng.Next(-5, 10), Give: [rng.Next(-1, 25), 0, 0, 0, 0], Take: [0, 0, 0, 0, rng.Next(-1, 25)], OtherPlayer: "unknown");
            var result = e.Execute(c); if (!result.Success) Assert.Equal(before, Snapshot(e)); e.Validate();
            if (e.TestState.Phase == Phase.Finished) break;
        }
    }
    [Fact]
    public void Maximum15RoadPiecesAreEnforced()
    {
        var e = Graph(Enumerable.Range(0, 16).Select(i => (i, i + 1, 0)).ToArray(), (0, 0));
        e.TestState.Roads.Clear(); Action(e, 0); Resources(e, 0, 19, 19, 0, 0, 0);
        for (int i = 0; i < 15; i++) OK(e, new(CommandKind.BuildRoad, "P0", i));
        Assert.Equal(15, e.TestState.Roads.Count); RejectedUnchanged(e, new(CommandKind.BuildRoad, "P0", 15));
    }
    [Fact]
    public void SettlementAndCityPieceLimitsAreEnforced()
    {
        var e = Graph(Enumerable.Range(0, 16).Select(i => (i, i + 1, 0)).ToArray()); e.TestState.Roads.Clear(); Action(e, 0);
        foreach (int v in new[] { 0, 3, 6, 9, 12 }) e.TestState.Buildings.Add(v, new(0, 1));
        Resources(e, 0, 1, 1, 1, 4, 3); e.TestState.Roads[14] = 0;
        RejectedUnchanged(e, new(CommandKind.BuildSettlement, "P0", 15));
        foreach (int v in new[] { 0, 3, 6, 9 }) e.TestState.Buildings[v] = new(0, 2);
        RejectedUnchanged(e, new(CommandKind.BuildCity, "P0", 12));
    }
    [Fact]
    public void RoadBuildingMayUseLastSingleRoadPiece()
    {
        var e = Started(); Action(e); string id = Active(e); int p = e.TestState.Active;
        // Reach 14 roads via legal free-card-style fixture placement without spending resources.
        while (e.TestState.Roads.Values.Count(o => o == p) < 14) e.TestState.Roads.Add(e.LegalRoads(id)[0], p);
        GrantCard(e, p, Card.RoadBuilding); OK(e, new(CommandKind.PlayRoadBuilding, id, e.LegalRoads(id)[0]));
        Assert.Equal(15, e.TestState.Roads.Values.Count(o => o == p));
    }
    [Fact]
    public void YearOfPlentyTakesOnlyRemainingCardWhenBankHasOne()
    {
        var e = Started(); Action(e, 0); GrantCard(e, 0, Card.YearOfPlenty);
        for (int p = 0; p < 4; p++) Resources(e, p, 0, 0, 0, 0, 0);
        Resources(e, 1, 18, 19, 19, 19, 19); Assert.Equal(1, e.TestState.Bank.Sum());
        OK(e, new(CommandKind.PlayYearOfPlenty, "P0", Take: [1, 0, 0, 0, 0])); Assert.Equal(0, e.TestState.Bank.Sum());
    }
    [Fact]
    public void EmptyDevelopmentDeckDoesNotSpendResources()
    {
        var e = Started(); Action(e); Resources(e, e.TestState.Active, 0, 0, 1, 1, 1);
        // Distribute the complete deck into other hands; total card inventory is unchanged.
        int other = (e.TestState.Active + 1) % 4; e.TestState.Players[other].Cards.AddRange(e.TestState.Deck); e.TestState.Deck.Clear();
        RejectedUnchanged(e, new(CommandKind.BuyCard, Active(e)));
    }
    [Fact]
    public void ActiveSurrenderAndAllSurrenderHaveNoWinnerAndCannotRemovePieces()
    {
        var e = Started(); Action(e, 0); int buildings = e.TestState.Buildings.Count; int roads = e.TestState.Roads.Count;
        foreach (string p in new[] { "P0", "P1", "P2", "P3" }) OK(e, new(CommandKind.Surrender, p));
        Assert.Equal(Phase.Abandoned, e.TestState.Phase); Assert.Null(e.View("P0").Winner);
        Assert.Equal(buildings, e.TestState.Buildings.Count); Assert.Equal(roads, e.TestState.Roads.Count);
        RejectedUnchanged(e, new(CommandKind.Reconnect, "P0"));
    }
    [Fact]
    public void SimultaneousDuplicateCommandsSpendResourcesOnce()
    {
        var e = Started(); Action(e); string p = Active(e); Resources(e, e.TestState.Active, 2, 2, 0, 0, 0);
        int roads = e.TestState.Roads.Count; var c = new Command(CommandKind.BuildRoad, p, e.LegalRoads(p)[0], RequestId: "retry");
        var results = new System.Collections.Concurrent.ConcurrentBag<Result>(); Parallel.For(0, 20, _ => results.Add(e.Execute(c)));
        Assert.All(results, r => Assert.True(r.Success)); Assert.Single(results.Distinct()); Assert.Equal(roads + 1, e.TestState.Roads.Count);
    }
}
