using Game.Contracts;
using Game.Engine;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class BuildingAndIntegrityTests
{
    [Fact]
    public void RoadsSpendExactCostsAndRejectWrongPlayerWrongPhaseOccupiedAndDisconnected()
    {
        var e = Started(); string p = Active(e); int pi = e.TestState.Active;
        Resources(e, pi, 3, 3, 0, 0, 0); int road = e.LegalRoads(p)[0];
        RejectedUnchanged(e, new(CommandKind.BuildRoad, p, road)); Action(e);
        string other = e.TestState.Players[(pi + 1) % 4].Id;
        RejectedUnchanged(e, new(CommandKind.BuildRoad, other, road));
        RejectedUnchanged(e, new(CommandKind.BuildRoad, p, e.TestState.Board!.Edges.First(x => !e.LegalRoads(p).Contains(x.Id) && !e.TestState.Roads.ContainsKey(x.Id)).Id));
        int bankBrick = e.TestState.Bank[0]; OK(e, new(CommandKind.BuildRoad, p, road));
        Assert.Equal(2, e.TestState.Players[pi].Resources[0]); Assert.Equal(bankBrick + 1, e.TestState.Bank[0]);
        RejectedUnchanged(e, new(CommandKind.BuildRoad, p, road));
        Resources(e, pi, 0, 0, 0, 0, 0); RejectedUnchanged(e, new(CommandKind.BuildRoad, p, e.LegalRoads(p)[0]));
    }
    [Fact]
    public void SetupRequiresSettlementDistanceAndRoadAttachedToJustPlacedSettlement()
    {
        var e = Lobby(); OK(e, new(CommandKind.Start, "P0")); string p = Active(e); int v = e.LegalSettlements(p, true)[0];
        OK(e, new(CommandKind.PlaceSettlement, p, v));
        RejectedUnchanged(e, new(CommandKind.PlaceRoad, p, e.TestState.Board!.Edges.First(edge => edge.A != v && edge.B != v).Id));
        OK(e, new(CommandKind.PlaceRoad, p, e.LegalRoads(p, true)[0])); p = Active(e);
        RejectedUnchanged(e, new(CommandKind.PlaceSettlement, p, v));
        RejectedUnchanged(e, new(CommandKind.PlaceSettlement, p, e.TestState.Board.Neighbors(v).First()));
    }
    [Fact]
    public void CityUpgradeReturnsSettlementAndDoublesProduction()
    {
        var e = Started(); Action(e); int p = e.TestState.Active; string id = Active(e);
        int vertex = e.TestState.Buildings.First(kv => kv.Value.Owner == p).Key;
        Resources(e, p, 0, 0, 0, 2, 3); OK(e, new(CommandKind.BuildCity, id, vertex));
        Assert.Equal(2, e.TestState.Buildings[vertex].Level); Assert.Equal(new int[5], e.TestState.Players[p].Resources);
        RejectedUnchanged(e, new(CommandKind.BuildCity, id, vertex));
        int other = e.TestState.Buildings.First(kv => kv.Value.Owner != p).Key;
        RejectedUnchanged(e, new(CommandKind.BuildCity, id, other));
        var hex = e.TestState.Board!.Hexes.First(h => h.Resource.HasValue && h.Vertices.Contains(vertex));
        e.TestState.Robber = e.TestState.Board.Hexes.First(h => h.Id != hex.Id).Id;
        GameEngine.Produce(e.TestState, hex.Number); Assert.True(e.TestState.Players[p].Resources[(int)hex.Resource!.Value] >= 2); e.Validate();
    }
    [Fact]
    public void SettlementConnectivityAndPieceInventoryAreEnforced()
    {
        var e = Started(); Action(e); int p = e.TestState.Active; string id = Active(e); Resources(e, p, 5, 5, 5, 5, 0);
        int unconnected = e.LegalSettlements(id, true).First(v => !e.LegalSettlements(id).Contains(v));
        RejectedUnchanged(e, new(CommandKind.BuildSettlement, id, unconnected));
        // Extend a road until a legal settlement is reachable.
        while (e.LegalSettlements(id).Count == 0)
        {
            int target = e.TestState.Board!.VertexEdges[unconnected].First();
            var candidates = e.LegalRoads(id); Assert.NotEmpty(candidates);
            int edge = candidates.OrderBy(x => Distance(e.TestState.Board!, x, target)).First();
            if (e.TestState.Players[p].Resources[0] == 0) Resources(e, p, 5, 5, 5, 5, 0);
            OK(e, new(CommandKind.BuildRoad, id, edge));
        }
        Resources(e, p, 1, 1, 1, 1, 0); int settlement = e.LegalSettlements(id)[0]; OK(e, new(CommandKind.BuildSettlement, id, settlement));
        Assert.Equal(new int[5], e.TestState.Players[p].Resources); RejectedUnchanged(e, new(CommandKind.BuildSettlement, id, settlement));
        static int Distance(Board board, int a, int b) => Math.Abs(board.Edges[a].A - board.Edges[b].A);
    }
    [Fact]
    public void OpponentBuildingInterruptsRoadConnectivity()
    {
        var e = Graph([(0, 1, 0), (1, 2, 0), (2, 3, 1)], (2, 1));
        e.TestState.Roads.Remove(1); e.TestState.Roads.Remove(2);
        Assert.Contains(1, e.LegalRoads("P0")); Assert.DoesNotContain(2, e.LegalRoads("P0"));
        e.TestState.Roads[1] = 0; Assert.DoesNotContain(2, e.LegalRoads("P0"));
    }
    [Fact]
    public void DuplicateCommandsAreIdempotentAndReusedIdsCannotChangeIntent()
    {
        var e = Started(); Action(e); string p = Active(e); Resources(e, e.TestState.Active, 3, 3, 0, 0, 0);
        var c = new Command(CommandKind.BuildRoad, p, e.LegalRoads(p)[0], RequestId: "one");
        var first = e.Execute(c); Assert.True(first.Success); string before = Snapshot(e);
        Assert.Equal(first, e.Execute(c)); Assert.Equal(before, Snapshot(e));
        RejectedUnchanged(e, c with { Target = e.LegalRoads(p)[0] });
    }
    [Fact]
    public void InvalidCommandCannotConsumeRandomnessAndGamesAreIsolated()
    {
        var a = Started(55); var b = Started(55); string p = Active(a);
        RejectedUnchanged(a, new(CommandKind.MoveRobber, p, 1000)); OK(a, new(CommandKind.Roll, p)); OK(b, new(CommandKind.Roll, p));
        Assert.Equal(a.TestState.Roll, b.TestState.Roll); Assert.NotEqual(a.TestState.Id, b.TestState.Id);
        Action(a); Action(b); Resources(a, a.TestState.Active, 2, 2, 0, 0, 0);
        string before = Snapshot(b); OK(a, new(CommandKind.BuildRoad, p, a.LegalRoads(p)[0])); Assert.Equal(before, Snapshot(b));
    }
    [Theory]
    [InlineData(-1)] [InlineData(1000)]
    public void InvalidLocationsAreAtomic(int location)
    {
        var e = Started(); Action(e); string p = Active(e); Resources(e, e.TestState.Active, 4, 4, 4, 4, 4);
        foreach (var kind in new[] { CommandKind.BuildRoad, CommandKind.BuildSettlement, CommandKind.BuildCity }) RejectedUnchanged(e, new(kind, p, location));
    }
    [Fact]
    public void ProjectionCannotLeakOrModifyPrivateEngineState()
    {
        var e = Started(); GrantCard(e, 1, Card.VictoryPoint); var view = e.View("P0");
        Assert.Null(view.Players[1].Resources); Assert.Null(view.Players[1].Cards); Assert.Null(view.Players[1].TotalPoints);
        Assert.NotNull(view.Players[0].Resources); string before = Snapshot(e);
        ((int[])view.Players[0].Resources!)[0] = 999; ((int[])view.Bank)[0] = 999;
        ((int[])view.Hexes[0].Vertices)[0] = 999; Assert.Equal(before, Snapshot(e));
    }
}
