using Game.Contracts;
using Game.Engine;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class BoardAndLobbyTests
{
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void LobbyReadyRollOffAndSnakeSetup(int count)
    {
        var e = Lobby(count: count); OK(e, new(CommandKind.Start, "P0"));
        int starting = e.TestState.Starting;
        var order = new List<int>(); var expectedResources = new Dictionary<int, int[]>();
        for (int step = 0; step < count * 2; step++)
        {
            int active = e.TestState.Active; string p = Active(e); order.Add(active);
            int v = e.LegalSettlements(p, true)[0]; OK(e, new(CommandKind.PlaceSettlement, p, v));
            if (step >= count)
            {
                var expected = new int[5];
                foreach (var h in e.TestState.Board!.Hexes.Where(h => h.Resource.HasValue && h.Vertices.Contains(v))) expected[(int)h.Resource!.Value]++;
                expectedResources[active] = expected;
            }
            OK(e, new(CommandKind.PlaceRoad, p, e.LegalRoads(p, true)[0]));
        }
        Assert.Equal(Enumerable.Range(0, count).Select(i => (starting + i) % count).Concat(Enumerable.Range(0, count).Reverse().Select(i => (starting + i) % count)), order);
        Assert.Equal(Phase.AwaitRoll, e.TestState.Phase); Assert.Equal(starting, e.TestState.Active);
        Assert.Equal(count * 2, e.TestState.Buildings.Count); Assert.Equal(count * 2, e.TestState.Roads.Count);
        for (int p = 0; p < count; p++) Assert.Equal(expectedResources[p], e.TestState.Players[p].Resources);
    }
    [Fact]
    public void LobbyRestrictions()
    {
        var e = new GameEngine(1, new ManualClock()); OK(e, new(CommandKind.AddPlayer, "P0")); OK(e, new(CommandKind.Ready, "P0"));
        RejectedUnchanged(e, new(CommandKind.Start, "P0")); OK(e, new(CommandKind.AddPlayer, "P1"));
        RejectedUnchanged(e, new(CommandKind.Start, "P0")); OK(e, new(CommandKind.Ready, "P1"));
        RejectedUnchanged(e, new(CommandKind.Start, "P1")); RejectedUnchanged(e, new(CommandKind.AddPlayer, "P1"));
        OK(e, new(CommandKind.Start, "P0")); RejectedUnchanged(e, new(CommandKind.AddPlayer, "P2"));
        var full = Lobby(); RejectedUnchanged(full, new(CommandKind.AddPlayer, "P4"));
        Assert.Throws<ArgumentException>(() => e.View("spectator"));
    }
    [Fact]
    public void GeneratedBoardsHaveCorrectTopologySupplyAndNoAdjacentRedTokens()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var e = Lobby(seed); OK(e, new(CommandKind.Start, "P0")); var view = e.View("P0");
            Assert.Equal(19, view.Hexes.Count); Assert.Equal(54, view.Vertices.Count); Assert.Equal(72, view.Edges.Count);
            Assert.Equal(9, view.Ports.Count); Assert.Equal(18, view.Ports.SelectMany(p => new[] { p.A, p.B }).Distinct().Count());
            Assert.Equal(1, view.Hexes.Count(h => h.Resource == null)); Assert.Equal(0, view.Hexes[view.RobberHex].Number);
            foreach (Resource r in Enum.GetValues<Resource>()) Assert.Equal(r is Resource.Brick or Resource.Ore ? 3 : 4, view.Hexes.Count(h => h.Resource == r));
            Assert.Equal(new[] { 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12 }, view.Hexes.Where(h => h.Number > 0).Select(h => h.Number).Order());
            foreach (var a in view.Hexes.Where(h => h.Number is 6 or 8))
                foreach (var b in view.Hexes.Where(h => h.Id != a.Id && h.Number is 6 or 8)) Assert.NotEqual(2, a.Vertices.Intersect(b.Vertices).Count());
            Assert.Equal(25, e.TestState.Deck.Count); Assert.Equal(14, e.TestState.Deck.Count(c => c.Kind == Card.Knight));
            Assert.All(view.Vertices, v => Assert.InRange(v.Edges.Count, 2, 3));
        }
    }
    [Fact]
    public void SeedReproducesBoardCardsAndStartingPlayer()
    {
        var a = Lobby(123); var b = Lobby(123); OK(a, new(CommandKind.Start, "P0")); OK(b, new(CommandKind.Start, "P0"));
        Assert.Equal(a.TestState.Starting, b.TestState.Starting); Assert.Equal(a.TestState.Deck, b.TestState.Deck);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(a.View("P0").Hexes), System.Text.Json.JsonSerializer.Serialize(b.View("P0").Hexes));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(a.View("P0").Ports), System.Text.Json.JsonSerializer.Serialize(b.View("P0").Ports));
    }
    [Fact]
    public void RandomRollOffRerollsOnlyTiedLeaders()
    {
        // Independent expected roll-off, including seeds with an initial tie.
        int observedTies = 0;
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var rng = new RandomSource(seed); _ = Board.Generate(rng); var candidates = new List<int> { 0, 1, 2, 3 };
            while (candidates.Count > 1)
            {
                var rolls = candidates.Select(p => (p, n: rng.Next(6) + rng.Next(6) + 2)).ToArray();
                int highest = rolls.Max(x => x.n); candidates = rolls.Where(x => x.n == highest).Select(x => x.p).ToList();
                if (candidates.Count > 1) observedTies++;
            }
            var e = Lobby(seed); OK(e, new(CommandKind.Start, "P0")); Assert.Equal(candidates[0], e.TestState.Starting);
        }
        Assert.True(observedTies > 0);
    }
}
