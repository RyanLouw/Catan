using Game.Contracts;
namespace Game.Engine;

public sealed partial class GameEngine
{
    /// <summary>Only the requesting player's private state is projected. The transport must authenticate this ID.</summary>
    public GameView View(string viewer)
    {
        lock (gate)
        {
            int owner = Find(state, viewer); if (owner < 0) throw new ArgumentException("Unknown player; spectators are not supported.", nameof(viewer));
            var s = state;
            return new(s.Id, s.RoomCode, s.Version, s.Phase, s.Players[0].Id,
                s.Phase == Phase.Lobby ? null : s.Players[s.Active].Id, s.Turn, s.Roll, s.Robber, s.Deadline,
                Name(s.Winner), Name(s.Longest), Name(s.Army),
                s.Board?.Hexes.Select(h => new HexView(h.Id, h.Q, h.R, h.Resource, h.Number, (int[])h.Vertices.Clone())).ToArray() ?? [],
                s.Board?.VertexEdges.Select((e, v) => new VertexView(v, (int[])e.Clone(), s.Buildings.TryGetValue(v, out var b) ? Name(b.Owner) : null, s.Buildings.GetValueOrDefault(v)?.Level ?? 0)).ToArray() ?? [],
                s.Board?.Edges.Select(e => new EdgeView(e.Id, e.A, e.B, Name(s.Roads.GetValueOrDefault(e.Id, -1)))).ToArray() ?? [],
                s.Board?.Ports.Select(p => new PortView(p.A, p.B, p.Resource)).ToArray() ?? [], (int[])s.Bank.Clone(),
                s.Players.Select((p, i) => new PlayerView(p.Id, p.Ready, p.Connected, p.Surrendered, p.Resources.Sum(), p.Cards.Count,
                    Points(s, i, false), p.Knights, RoadLength(s, i), i == owner ? (int[])p.Resources.Clone() : null,
                    i == owner ? p.Cards.Select(c => c.Kind).ToArray() : null, i == owner ? Points(s, i, true) : null)).ToArray(),
                s.Trades.Select(t => new TradeView(t.Id, s.Players[t.From].Id, s.Players[t.To].Id, (int[])t.Give.Clone(), (int[])t.Take.Clone())).ToArray(),
                s.Discards.ToDictionary(kv => s.Players[kv.Key].Id, kv => kv.Value), s.Victims.Select(v => s.Players[v].Id).ToArray(), s.Deck.Count, s.ActionDeadline, s.RollDeadline);
            string? Name(int p) => p < 0 ? null : s.Players[p].Id;
        }
    }
    public IReadOnlyList<int> LegalRoads(string player, bool setup = false)
    {
        lock (gate)
        {
            int p = Find(state, player); if (p < 0 || state.Board == null) return [];
            return Enumerable.Range(0, state.Board.Edges.Length).Where(e => LegalRoad(state, p, e, setup)).ToArray();
        }
    }
    public IReadOnlyList<int> LegalSettlements(string player, bool setup = false)
    {
        lock (gate)
        {
            int p = Find(state, player); if (p < 0 || state.Board == null) return [];
            return Enumerable.Range(0, state.Board.VertexEdges.Length).Where(v => LegalSettlement(state, p, v, setup)).ToArray();
        }
    }
    public int TradeRatio(string player, Resource resource)
    {
        lock (gate)
        {
            int p = Find(state, player); if (p < 0 || state.Board == null || !Enum.IsDefined(resource)) throw new ArgumentException("Invalid player or resource.");
            return Ratio(state, p, resource);
        }
    }
}
