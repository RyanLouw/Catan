using Game.Contracts;
namespace Game.Engine;

public sealed record SimulationResult(ulong Seed, int Players, string? Winner, int Turns, int Commands);
/// <summary>Headless legal-intent driver, also usable as a console demonstration. No privileged state access.</summary>
public static class Simulation
{
    public static SimulationResult Run(ulong seed, int players = 4, int maxTurns = 3000)
    {
        try { return RunCore(seed, players, maxTurns); }
        catch (InvalidOperationException e) { throw new InvalidOperationException($"Simulation failed: seed={seed}, players={players}. {e.Message}", e); }
    }
    private static SimulationResult RunCore(ulong seed, int players, int maxTurns)
    {
        if (players is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(players));
        var clock = new ManualClock(); var engine = new GameEngine(seed, clock);
        var names = Enumerable.Range(1, players).Select(i => $"P{i}").ToArray();
        foreach (string p in names) { Must(engine, new(CommandKind.AddPlayer, p)); Must(engine, new(CommandKind.Ready, p)); }
        Must(engine, new(CommandKind.Start, names[0]));
        int commands = 0;
        while (true)
        {
            var publicView = engine.View(names[0]);
            if (publicView.Phase == Phase.Finished) return new(seed, players, publicView.Winner, publicView.Turn, commands);
            if (publicView.Phase == Phase.Abandoned || publicView.Turn > maxTurns || commands > maxTurns * 80)
                throw new InvalidOperationException($"Simulation did not finish: seed={seed}, players={players}, turn={publicView.Turn}, phase={publicView.Phase}.");
            string p = publicView.ActivePlayer!; var view = engine.View(p); var me = view.Players.Single(x => x.Id == p);
            Command? command;
            switch (view.Phase)
            {
                case Phase.SetupSettlement:
                    command = new(CommandKind.PlaceSettlement, p, BestSettlement(view, engine.LegalSettlements(p, true))); break;
                case Phase.SetupRoad:
                    command = new(CommandKind.PlaceRoad, p, BestRoad(engine, view, p, engine.LegalRoads(p, true))); break;
                case Phase.AwaitRoll: command = new(CommandKind.Roll, p); break;
                case Phase.Discard:
                    p = view.RequiredDiscards.Keys.Order().First(); var resources = engine.View(p).Players.Single(x => x.Id == p).Resources!.ToArray();
                    var give = new int[5];
                    for (int n = 0; n < view.RequiredDiscards[p]; n++) { int r = Array.IndexOf(resources, resources.Max()); give[r]++; resources[r]--; }
                    command = new(CommandKind.Discard, p, Give: give); break;
                case Phase.Robber:
                    int target = view.Hexes.Where(h => h.Id != view.RobberHex).OrderByDescending(h =>
                        h.Vertices.Sum(v => view.Vertices[v].Owner == p ? -20 : view.Vertices[v].Level * 3) + (h.Number == 0 ? 0 : 6 - Math.Abs(7 - h.Number))).ThenBy(h => h.Id).First().Id;
                    command = new(CommandKind.MoveRobber, p, target); break;
                case Phase.Victim:
                    command = new(CommandKind.ChooseVictim, p, OtherPlayer: view.EligibleVictims.OrderByDescending(v => view.Players.Single(x => x.Id == v).ResourceCount).First()); break;
                case Phase.Action:
                    command = ChooseAction(engine, view, me); break;
                default: throw new InvalidOperationException($"Unexpected simulation phase {view.Phase}.");
            }
            if (command != null) Must(engine, command); commands++; engine.Validate();
        }
    }
    private static void Must(GameEngine engine, Command command)
    {
        var result = engine.Execute(command);
        if (!result.Success) throw new InvalidOperationException($"Bot submitted {command}: {result.Message}");
    }
    private static int BestSettlement(GameView view, IReadOnlyList<int> legal) => legal.OrderByDescending(v => Value(view, v)).ThenBy(v => v).First();
    private static double Value(GameView view, int vertex)
    {
        var hexes = view.Hexes.Where(h => h.Vertices.Contains(vertex) && h.Resource.HasValue).ToArray();
        return hexes.Sum(h => 6 - Math.Abs(7 - h.Number)) + hexes.Select(h => h.Resource).Distinct().Count() * 0.7
            + (view.Ports.Any(p => p.A == vertex || p.B == vertex) ? 0.6 : 0);
    }
    private static int BestRoad(GameEngine engine, GameView view, string p, IReadOnlyList<int> legal)
    {
        var targets = engine.LegalSettlements(p, true).ToHashSet();
        if (targets.Count == 0) return legal[0];
        double Score(int edge)
        {
            var e = view.Edges[edge]; var seen = new HashSet<int> { e.A, e.B }; var queue = new Queue<(int Vertex, int Distance)>();
            queue.Enqueue((e.A, 0)); queue.Enqueue((e.B, 0)); double best = -100;
            while (queue.TryDequeue(out var item))
            {
                if (targets.Contains(item.Vertex)) best = Math.Max(best, Value(view, item.Vertex) - item.Distance * 5);
                if (view.Vertices[item.Vertex].Owner is string owner && owner != p) continue;
                foreach (int next in view.Vertices[item.Vertex].Edges)
                {
                    var road = view.Edges[next]; if (road.Owner != null && road.Owner != p) continue;
                    int v = road.A == item.Vertex ? road.B : road.A;
                    if (seen.Add(v)) queue.Enqueue((v, item.Distance + (road.Owner == p ? 0 : 1)));
                }
            }
            return best;
        }
        return legal.OrderByDescending(Score).ThenBy(e => e).First();
    }
    private static Command? ChooseAction(GameEngine engine, GameView view, PlayerView me)
    {
        string p = me.Id; int[] resources = me.Resources!.ToArray();
        bool Can(int[] cost) => cost.Where((n, i) => n > resources[i]).Any() == false;
        // Try playable cards through the engine: acquisition-age and per-turn restrictions remain authoritative.
        foreach (Card card in me.Cards!.Distinct())
        {
            Command? play = card switch
            {
                Card.Knight => new(CommandKind.PlayKnight, p),
                Card.Monopoly => new(CommandKind.PlayMonopoly, p, Resource: (Resource)Array.IndexOf(resources, resources.Min())),
                Card.YearOfPlenty => new(CommandKind.PlayYearOfPlenty, p, Take: Plenty(view, resources)),
                _ => null
            };
            if (play != null && engine.Execute(play).Success) return null;
            if (card == Card.RoadBuilding)
            {
                var roads = engine.LegalRoads(p);
                foreach (int a in roads)
                {
                    foreach (int b in Enumerable.Range(0, view.Edges.Count).Where(b => b != a).Append(-1))
                        if (engine.Execute(new(CommandKind.PlayRoadBuilding, p, a, b)).Success) return null;
                }
            }
        }
        var settlements = engine.LegalSettlements(p); var roadsLegal = engine.LegalRoads(p);
        int[] cities = view.Vertices.Where(v => v.Owner == p && v.Level == 1).Select(v => v.Id).ToArray();
        if (Can(Costs.Settlement) && settlements.Count > 0) return new(CommandKind.BuildSettlement, p, BestSettlement(view, settlements));
        if (Can(Costs.City) && cities.Length > 0 && view.Vertices.Count(v => v.Owner == p && v.Level == 2) < 4)
            return new(CommandKind.BuildCity, p, BestSettlement(view, cities));
        if (Can(Costs.Road) && roadsLegal.Count > 0 && settlements.Count == 0 && engine.LegalSettlements(p, true).Count > 0)
            return new(CommandKind.BuildRoad, p, BestRoad(engine, view, p, roadsLegal));
        // Trade towards a useful build or card, using the engine's harbor ratio.
        var goals = new List<int[]>();
        if (settlements.Count > 0) goals.Add(Costs.Settlement);
        if (cities.Length > 0 && view.Vertices.Count(v => v.Owner == p && v.Level == 2) < 4) goals.Add(Costs.City);
        if (view.DevelopmentDeckCount > 0) goals.Add(Costs.Development);
        if (roadsLegal.Count > 0 && settlements.Count == 0) goals.Add(Costs.Road);
        foreach (int[] goal in goals.OrderBy(g => g.Where((n, i) => n > resources[i]).Sum()))
        {
            for (int need = 0; need < 5; need++) if (resources[need] < goal[need] && view.Bank[need] > 0)
                for (int surplus = 0; surplus < 5; surplus++)
                {
                    int ratio = engine.TradeRatio(p, (Resource)surplus);
                    if (surplus != need && resources[surplus] - goal[surplus] >= ratio)
                    {
                        var give = new int[5]; var take = new int[5]; give[surplus] = ratio; take[need] = 1;
                        return new(CommandKind.BankTrade, p, Give: give, Take: take);
                    }
                }
        }
        if (Can(Costs.Development))
        {
            var buy = new Command(CommandKind.BuyCard, p);
            if (engine.Execute(buy).Success) return null;
        }
        return new(CommandKind.EndTurn, p);
    }
    private static int[] Plenty(GameView view, int[] resources)
    {
        var bank = view.Bank.ToArray(); var take = new int[5];
        for (int n = 0; n < Math.Min(2, view.Bank.Sum()); n++)
        {
            int r = Enumerable.Range(0, 5).Where(i => bank[i] > 0).OrderBy(i => resources[i] + take[i]).ThenBy(i => i).First(); take[r]++; bank[r]--;
        }
        return take;
    }
}
