using Game.Contracts;
namespace Game.Engine;

public sealed partial class GameEngine
{
    /// <summary>Called by the authoritative host, independently of client commands.</summary>
    public void Tick()
    {
        lock (gate) TickCore(clock.Milliseconds);
    }
    private void TickCore(long now)
    {
        var next = state.Clone(); bool changed = false;
        // Disconnected-only games retain 10s grace per turn. Bound work per host tick.
        for (int steps = 0; steps < 10_000; steps++)
        {
            long? due = next.Deadline;
            if (next.Phase is Phase.Robber or Phase.Victim && next.ActionDeadline.HasValue)
                due = Math.Min(due ?? long.MaxValue, next.ActionDeadline.Value);
            if (!due.HasValue || due > now) break;
            Auto(next, due.Value); NormalizeSurrender(next, due.Value); UpdateAwards(next); CheckVictory(next); AssertInvariants(next);
            next.Version++; changed = true;
        }
        if (changed) state = next;
    }
    private static void Auto(State s, long now)
    {
        switch (s.Phase)
        {
            case Phase.SetupSettlement:
                var vertices = Enumerable.Range(0, s.Board!.VertexEdges.Length).Where(v => LegalSettlement(s, s.Active, v, true)).ToArray();
                Require(vertices.Length > 0, "No initial settlement placement remains.");
                int vertex = vertices[s.Random.Next(vertices.Length)]; PlaceSettlement(s, s.Active, vertex, true); s.SetupVertex = vertex;
                s.Phase = Phase.SetupRoad; s.Deadline = now + 30_000; break;
            case Phase.SetupRoad:
                var edges = Enumerable.Range(0, s.Board!.Edges.Length).Where(e => LegalRoad(s, s.Active, e, true)).ToArray();
                Require(edges.Length > 0, "No initial road placement remains."); PlaceRoad(s, s.Active, edges[s.Random.Next(edges.Length)], true); NextSetup(s, now); break;
            case Phase.ReconnectGrace: NextTurn(s, now); break;
            case Phase.AwaitRoll: ResolveRoll(s, now); break;
            case Phase.Discard:
                foreach (var (p, n) in s.Discards.OrderBy(kv => kv.Key).ToArray())
                {
                    for (int i = 0; i < n; i++)
                    {
                        int choice = s.Random.Next(s.Players[p].Resources.Sum());
                        for (int r = 0; r < 5; r++)
                            if (choice < s.Players[p].Resources[r]) { s.Players[p].Resources[r]--; s.Bank[r]++; break; } else choice -= s.Players[p].Resources[r];
                    }
                }
                s.Discards.Clear(); BeginRobber(s, now); break;
            case Phase.Robber:
                int h = s.Random.Next(18); if (h >= s.Robber) h++; MoveRobber(s, h, now);
                if (s.ActionDeadline <= now && s.Phase == Phase.Victim) { Steal(s, s.Victims[s.Random.Next(s.Victims.Count)]); ReturnFromRobber(s, now); }
                break;
            case Phase.Victim: Steal(s, s.Victims[s.Random.Next(s.Victims.Count)]); ReturnFromRobber(s, now); break;
            case Phase.Action: NextTurn(s, now); break;
        }
    }
    private static void NormalizeSurrender(State s, long now)
    {
        if (s.Players.Count == 0 || !s.Players[s.Active].Surrendered) return;
        while (s.Phase is Phase.Robber or Phase.Victim) Auto(s, now);
        if (s.Phase is Phase.Action or Phase.AwaitRoll or Phase.ReconnectGrace) NextTurn(s, now);
    }
    private static void Surrender(State s, int p, long now)
    {
        s.Players[p].Surrendered = true; s.Trades.RemoveAll(t => t.From == p || t.To == p);
        if (s.Players.All(x => x.Surrendered)) { s.Phase = Phase.Abandoned; s.Deadline = null; return; }
        if (s.Phase == Phase.Lobby) return;
        if (s.Discards.TryGetValue(p, out int count))
        {
            for (int i = 0; i < count; i++)
            {
                int r = Array.FindIndex(s.Players[p].Resources, x => x > 0); s.Players[p].Resources[r]--; s.Bank[r]++;
            }
            s.Discards.Remove(p); if (s.Discards.Count == 0) BeginRobber(s, now);
        }
        if (p != s.Active) return;
        if (s.Phase == Phase.SetupSettlement) { NextSetup(s, now); return; }
        if (s.Phase == Phase.SetupRoad) { Auto(s, now); return; }
        // Finish mandatory roll effects before skipping the surrendered player's turn.
        if (s.Phase == Phase.Discard) return;
        while (s.Phase is Phase.Robber or Phase.Victim) Auto(s, now);
        NextTurn(s, now);
    }
}
