using Game.Contracts;
namespace Game.Engine;

public sealed partial class GameEngine
{
    private static void Start(State s, long now)
    {
        s.Started = true; s.Board = Board.Generate(s.Random); s.Robber = Array.FindIndex(s.Board.Hexes, h => h.Resource == null);
        var tied = Enumerable.Range(0, s.Players.Count).ToList();
        while (tied.Count > 1)
        {
            var rolls = tied.Select(p => (Player: p, Roll: s.Random.Next(6) + s.Random.Next(6) + 2)).ToArray();
            int max = rolls.Max(x => x.Roll); tied = rolls.Where(x => x.Roll == max).Select(x => x.Player).ToList();
        }
        // Rotate seating after the roll-off; the host remains separately identified by original ID.
        s.Starting = tied[0]; s.Active = tied[0];
        foreach (var (card, count) in new[] { (Card.Knight, 14), (Card.VictoryPoint, 5), (Card.RoadBuilding, 2), (Card.YearOfPlenty, 2), (Card.Monopoly, 2) })
            s.Deck.AddRange(Enumerable.Repeat(new HeldCard(card, -1), count));
        s.Random.Shuffle(s.Deck); s.Phase = Phase.SetupSettlement; s.Deadline = now + s.Timing.MandatoryMilliseconds;
    }
    private static bool LegalSettlement(State s, int p, int vertex, bool setup)
    {
        if (s.Board == null || vertex < 0 || vertex >= s.Board.VertexEdges.Length || s.Buildings.ContainsKey(vertex)) return false;
        if (s.Buildings.Values.Count(b => b.Owner == p && b.Level == 1) >= 5) return false;
        if (s.Board.Neighbors(vertex).Any(s.Buildings.ContainsKey)) return false;
        return setup || s.Board.VertexEdges[vertex].Any(e => s.Roads.GetValueOrDefault(e, -1) == p);
    }
    private static void PlaceSettlement(State s, int p, int vertex, bool setup)
    {
        Require(LegalSettlement(s, p, vertex, setup), "Illegal settlement location, connectivity, distance or piece inventory.");
        s.Buildings.Add(vertex, new(p, 1));
        if (setup && s.SetupStep >= s.Players.Count)
        {
            var resources = new int[5];
            foreach (var h in s.Board!.Hexes.Where(h => h.Resource.HasValue && h.Vertices.Contains(vertex))) resources[(int)h.Resource!.Value]++;
            TakeBank(s, p, resources);
        }
    }
    private static bool LegalRoad(State s, int p, int edge, bool setup)
    {
        if (s.Board == null || edge < 0 || edge >= s.Board.Edges.Length || s.Roads.ContainsKey(edge) || s.Roads.Values.Count(o => o == p) >= 15) return false;
        var e = s.Board.Edges[edge];
        if (setup) return e.A == s.SetupVertex || e.B == s.SetupVertex;
        return Connected(e.A) || Connected(e.B);
        bool Connected(int v)
        {
            if (s.Buildings.TryGetValue(v, out var b)) return b.Owner == p;
            return s.Board.VertexEdges[v].Any(adj => s.Roads.GetValueOrDefault(adj, -1) == p);
        }
    }
    private static void PlaceRoad(State s, int p, int edge, bool setup)
    {
        Require(LegalRoad(s, p, edge, setup), "Illegal road location, connectivity or piece inventory."); s.Roads.Add(edge, p);
    }
    private static void NextSetup(State s, long now)
    {
        s.SetupStep++; s.SetupVertex = -1;
        if (s.SetupStep == s.Players.Count * 2) { s.Active = s.Starting; s.Turn = 1; BeginTurn(s, now); return; }
        int seat = s.SetupStep < s.Players.Count ? s.SetupStep : s.Players.Count * 2 - 1 - s.SetupStep;
        int previous=s.Active;
        s.Active = (s.Starting + seat) % s.Players.Count; s.Phase = Phase.SetupSettlement; s.Deadline = now + s.Timing.MandatoryMilliseconds;
        if(s.Active!=previous&&!s.Players[s.Active].Surrendered)BeginHandover(s,Phase.SetupSettlement,now);
        if (s.Players[s.Active].Surrendered) NextSetup(s, now);
    }
    private static void ResolveRoll(State s, long now)
    {
        s.FirstDie=s.Random.Next(6)+1;s.SecondDie=s.Random.Next(6)+1;
        s.Roll=s.FirstDie+s.SecondDie;s.Production.Clear();s.RobberReturn=Phase.Action;
        if (s.Roll == 7)
        {
            s.Discards = s.Players.Select((p, i) => (i, Count: p.Resources.Sum())).Where(x => x.Count > 7).ToDictionary(x => x.i, x => x.Count / 2);
            if (s.Discards.Count > 0) { s.Phase = Phase.Discard; s.Deadline = now + s.Timing.MandatoryMilliseconds; }
            else BeginRobber(s, now);
        }
        else
        {
            var before=s.Players.Select(p=>(int[])p.Resources.Clone()).ToArray();
            Produce(s,s.Roll.Value);
            for(int p=0;p<s.Players.Count;p++)s.Production[p]=Enumerable.Range(0,5).Select(r=>s.Players[p].Resources[r]-before[p][r]).ToArray();
            BeginAction(s,now);
        }
    }
    internal static void Produce(State s, int roll)
    {
        var claims = new int[s.Players.Count, 5];
        foreach (var h in s.Board!.Hexes.Where(h => h.Number == roll && h.Id != s.Robber && h.Resource.HasValue))
            foreach (int v in h.Vertices)
                if (s.Buildings.TryGetValue(v, out var b)) claims[b.Owner, (int)h.Resource!.Value] += b.Level;
        for (int r = 0; r < 5; r++)
        {
            var claimants = Enumerable.Range(0, s.Players.Count).Where(p => claims[p, r] > 0).ToArray();
            int total = claimants.Sum(p => claims[p, r]);
            // Classic shortage rule: nobody receives that type if several players are affected;
            // a sole claimant receives all remaining cards.
            if (total > s.Bank[r] && claimants.Length > 1) continue;
            foreach (int p in claimants) { int n = Math.Min(claims[p, r], s.Bank[r]); s.Players[p].Resources[r] += n; s.Bank[r] -= n; }
        }
    }
    private static void BeginRobber(State s, long now) { s.Phase = Phase.Robber; s.Deadline = now + s.Timing.MandatoryMilliseconds; }
    private static void MoveRobber(State s, int hex, long now)
    {
        Require(hex >= 0 && hex < s.Board!.Hexes.Length && hex != s.Robber, "Move robber to a different hex."); s.Robber = hex;
        s.Victims = s.Board!.Hexes[hex].Vertices.Where(s.Buildings.ContainsKey).Select(v => s.Buildings[v].Owner)
            .Distinct().Where(p => p != s.Active && s.Players[p].Resources.Sum() > 0).Order().ToList();
        if (s.Victims.Count == 0) ReturnFromRobber(s, now);
        else if (s.Victims.Count == 1) { Steal(s, s.Victims[0]); ReturnFromRobber(s, now); }
        else { s.Phase = Phase.Victim; s.Deadline = now + s.Timing.MandatoryMilliseconds; }
    }
    private static void Steal(State s, int victim)
    {
        var resources = s.Players[victim].Resources; int choice = s.Random.Next(resources.Sum());
        for (int r = 0; r < 5; r++) { if (choice < resources[r]) { resources[r]--; s.Players[s.Active].Resources[r]++; break; } choice -= resources[r]; }
        s.Victims.Clear();
    }
    private static void ReturnFromRobber(State s, long now)
    {
        s.Victims.Clear();
        if (s.RobberReturn == Phase.AwaitRoll) { s.Phase = Phase.AwaitRoll; s.Deadline = Math.Max(now, s.RollDeadline ?? now); }
        else if (s.ActionDeadline.HasValue) { s.Phase = Phase.Action; s.Deadline = s.ActionDeadline; }
        else BeginAction(s, now);
    }
    private static void BeginAction(State s, long now) { s.Phase = Phase.Action; s.ActionDeadline = now + s.Timing.ActionMilliseconds; s.Deadline = s.ActionDeadline; }
    private static void BeginRoll(State s, long now) { s.Phase = Phase.AwaitRoll; s.RollDeadline = now + s.Timing.RollMilliseconds; s.Deadline = s.RollDeadline; }
    private static void BeginTurn(State s, long now)
    {
        s.Roll = null; s.FirstDie = null; s.SecondDie = null; s.Production.Clear(); s.PlayedCard = false; s.ActionDeadline = null; s.RollDeadline = null; s.Trades.Clear();
        if (s.Players.All(p => p.Surrendered)) { s.Phase = Phase.Abandoned; s.Deadline = null; return; }
        if (s.Players[s.Active].Surrendered) { NextTurn(s, now); return; }
        if (!s.Players[s.Active].Connected) { s.Phase = Phase.ReconnectGrace; s.Deadline = now + 10_000; }
        else BeginHandover(s,Phase.AwaitRoll,now);
        CheckVictory(s);
    }
    private static void BeginHandover(State s,Phase next,long now)
    {
        if(s.Timing.HandoverMilliseconds==0){s.NextPhase=next;ResumeHandover(s,now);return;}
        s.Phase=Phase.Handover;s.NextPhase=next;s.Deadline=now+s.Timing.HandoverMilliseconds;
    }
    private static void ResumeHandover(State s,long now)
    {
        Phase next=s.NextPhase??Phase.AwaitRoll;s.NextPhase=null;
        if(next==Phase.AwaitRoll)BeginRoll(s,now);
        else {s.Phase=next;s.Deadline=now+s.Timing.MandatoryMilliseconds;}
    }
    private static void NextTurn(State s, long now) { s.Active = (s.Active + 1) % s.Players.Count; s.Turn++; BeginTurn(s, now); }
    private static int Ratio(State s, int p, Resource resource)
    {
        int ratio = 4;
        foreach (var port in s.Board!.Ports.Where(port => new[] { port.A, port.B }.Any(v => s.Buildings.TryGetValue(v, out var b) && b.Owner == p)))
            if (port.Resource == resource) ratio = 2; else if (port.Resource == null) ratio = Math.Min(ratio, 3);
        return ratio;
    }
    private static int Points(State s, int p, bool hidden) => s.Buildings.Values.Where(b => b.Owner == p).Sum(b => b.Level)
        + (s.Longest == p ? 2 : 0) + (s.Army == p ? 2 : 0) + (hidden ? s.Players[p].Cards.Count(c => c.Kind == Card.VictoryPoint) : 0);
    private static void CheckVictory(State s)
    {
        if (s.Phase is Phase.Lobby or Phase.SetupSettlement or Phase.SetupRoad or Phase.Finished or Phase.Abandoned) return;
        if (!s.Players[s.Active].Surrendered && Points(s, s.Active, true) >= 10)
        { s.Winner = s.Active; s.Phase = Phase.Finished; s.Deadline = null; s.ActionDeadline = null; s.RollDeadline = null; s.Trades.Clear(); }
    }
    internal static void UpdateAwards(State s)
    {
        if (s.Board == null) return;
        int[] roads = Enumerable.Range(0, s.Players.Count).Select(p => RoadLength(s, p)).ToArray();
        s.Longest = Award(roads, s.Longest, 5);
        s.Army = Award(s.Players.Select(p => p.Knights).ToArray(), s.Army, 3);
    }
    private static int Award(int[] values, int owner, int minimum)
    {
        int max = values.Max(); if (max < minimum) return -1;
        if (owner >= 0 && values[owner] == max) return owner;
        int[] leaders = Enumerable.Range(0, values.Length).Where(p => values[p] == max).ToArray();
        return leaders.Length == 1 ? leaders[0] : -1;
    }
    internal static int RoadLength(State s, int p)
    {
        if (s.Board == null) return 0;
        var owned = s.Roads.Where(kv => kv.Value == p).Select(kv => kv.Key).ToHashSet();
        int best = 0;
        foreach (int vertex in owned.SelectMany(e => new[] { s.Board.Edges[e].A, s.Board.Edges[e].B }).Distinct()) Walk(vertex, [], 0);
        return best;
        void Walk(int vertex, HashSet<int> used, int length)
        {
            best = Math.Max(best, length);
            if (length > 0 && s.Buildings.TryGetValue(vertex, out var building) && building.Owner != p) return;
            foreach (int edge in s.Board.VertexEdges[vertex])
                if (owned.Contains(edge) && used.Add(edge)) { Walk(s.Board.Other(edge, vertex), used, length + 1); used.Remove(edge); }
        }
    }
    public void Validate()
    {
        lock (gate) AssertInvariants(state);
    }
    private static void AssertInvariants(State s)
    {
        for (int r = 0; r < 5; r++)
            if (s.Bank[r] < 0 || s.Players.Any(p => p.Resources[r] < 0) || s.Bank[r] + s.Players.Sum(p => p.Resources[r]) != 19)
                throw new InvalidOperationException("Resource conservation violated.");
        if (s.Started)
        {
            foreach (var (kind, expected) in new[] { (Card.Knight, 14), (Card.VictoryPoint, 5), (Card.RoadBuilding, 2), (Card.YearOfPlenty, 2), (Card.Monopoly, 2) })
            {
                int total = s.Deck.Count(c => c.Kind == kind) + s.Players.Sum(p => p.Cards.Count(c => c.Kind == kind)) + s.UsedCards.Count(c => c == kind);
                if (total != expected) throw new InvalidOperationException("Development-card conservation violated.");
            }
            if (s.Players.Sum(p => p.Knights) != s.UsedCards.Count(c => c == Card.Knight)) throw new InvalidOperationException("Knight inventory violated.");
        }
        if (s.Board == null) return;
        foreach (var (v, b) in s.Buildings)
            if (v < 0 || v >= s.Board.VertexEdges.Length || b.Level is < 1 or > 2 || b.Owner < 0 || b.Owner >= s.Players.Count || s.Board.Neighbors(v).Any(s.Buildings.ContainsKey))
                throw new InvalidOperationException("Building invariant violated.");
        foreach (var (e, p) in s.Roads)
            if (e < 0 || e >= s.Board.Edges.Length || p < 0 || p >= s.Players.Count) throw new InvalidOperationException("Road invariant violated.");
        for (int p = 0; p < s.Players.Count; p++)
            if (s.Roads.Values.Count(o => o == p) > 15 || s.Buildings.Values.Count(b => b.Owner == p && b.Level == 1) > 5 || s.Buildings.Values.Count(b => b.Owner == p && b.Level == 2) > 4)
                throw new InvalidOperationException("Piece inventory violated.");
    }
}
