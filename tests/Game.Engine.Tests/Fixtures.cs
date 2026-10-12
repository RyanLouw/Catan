using Game.Contracts;
using Game.Engine;
using Xunit;
namespace Game.Engine.Tests;

internal static class Fixtures
{
    public static GameEngine Lobby(ulong seed = 1, int count = 4, IClock? clock = null)
    {
        var engine = new GameEngine(seed, clock ?? new ManualClock());
        for (int i = 0; i < count; i++) { OK(engine, new(CommandKind.AddPlayer, $"P{i}")); OK(engine, new(CommandKind.Ready, $"P{i}")); }
        return engine;
    }
    public static GameEngine Started(ulong seed = 1, int count = 4, IClock? clock = null)
    {
        var engine = Lobby(seed, count, clock); OK(engine, new(CommandKind.Start, "P0"));
        while (engine.TestState.Phase is Phase.SetupSettlement or Phase.SetupRoad)
        {
            string active = engine.TestState.Players[engine.TestState.Active].Id;
            if (engine.TestState.Phase == Phase.SetupSettlement) OK(engine, new(CommandKind.PlaceSettlement, active, engine.LegalSettlements(active, true)[0]));
            else OK(engine, new(CommandKind.PlaceRoad, active, engine.LegalRoads(active, true)[0]));
        }
        return engine;
    }
    public static string Active(GameEngine e) => e.TestState.Players[e.TestState.Active].Id;
    public static void Action(GameEngine e, int? active = null)
    {
        if (active.HasValue) e.TestState.Active = active.Value;
        e.TestState.Phase = Phase.Action; e.TestState.ActionDeadline = 90_000; e.TestState.Deadline = 90_000;
    }
    public static void Resources(GameEngine e, int player, params int[] values)
    {
        for (int r = 0; r < 5; r++)
        {
            e.TestState.Bank[r] += e.TestState.Players[player].Resources[r]; e.TestState.Players[player].Resources[r] = 0;
            Assert.True(values[r] <= e.TestState.Bank[r]); e.TestState.Bank[r] -= values[r]; e.TestState.Players[player].Resources[r] = values[r];
        }
    }
    public static void GrantCard(GameEngine e, int p, Card kind, bool boughtThisTurn = false)
    {
        int index = e.TestState.Deck.FindIndex(c => c.Kind == kind); Assert.True(index >= 0);
        e.TestState.Deck.RemoveAt(index); e.TestState.Players[p].Cards.Add(new(kind, boughtThisTurn ? e.TestState.Turn : e.TestState.Turn - 1));
    }
    public static void OK(GameEngine e, Command c) => Assert.True(e.Execute(c).Success, c.ToString());
    public static void RejectedUnchanged(GameEngine e, Command c)
    {
        string before = Snapshot(e); var result = e.Execute(c); Assert.False(result.Success, c.ToString()); Assert.Equal(before, Snapshot(e)); e.Validate();
    }
    public static string Snapshot(GameEngine e) => System.Text.Json.JsonSerializer.Serialize(e.TestState.Players.Select(p => e.View(p.Id)).ToArray());
    public static GameEngine Graph((int A, int B, int Owner)[] edges, params (int Vertex, int Owner)[] buildings)
    {
        var e = Lobby(count: 2); var s = e.TestState;
        int n = edges.Max(edge => Math.Max(edge.A, edge.B)) + 1;
        s.Board = new Board { Hexes = [], Ports = [], Edges = edges.Select((edge, i) => new Edge(i, edge.A, edge.B)).ToArray(),
            VertexEdges = Enumerable.Range(0, n).Select(v => edges.Select((edge, i) => (edge, i)).Where(x => x.edge.A == v || x.edge.B == v).Select(x => x.i).ToArray()).ToArray() };
        s.Roads = edges.Select((edge, i) => (edge, i)).ToDictionary(x => x.i, x => x.edge.Owner);
        s.Buildings = buildings.ToDictionary(x => x.Vertex, x => new Building(x.Owner, 1));
        return e;
    }
}
