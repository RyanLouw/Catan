using System.Text.Json;
using Game.Contracts;
namespace Game.Engine;

/// <summary>One isolated game. The host calls Tick with an injected authoritative clock; clients submit intents.</summary>
public sealed partial class GameEngine
{
    private readonly object gate = new();
    private readonly IClock clock;
    private State state;
    private readonly Dictionary<(string Player, string Id), (string Payload, Result Result)> receipts = [];
    public GameEngine(ulong seed, IClock? clock = null)
    {
        this.clock = clock ?? new SystemClock();
        state = new State { Random = new RandomSource(seed) };
        // Unique identity independent of gameplay RNG. Online room-code allocation must check collisions.
        state.RoomCode = state.Id.ToString("N")[..8].ToUpperInvariant();
    }
    internal State TestState { get => state; set => state = value; }
    public Result Execute(Command command)
    {
        lock (gate)
        {
            TickCore(clock.Milliseconds);
            string fingerprint = JsonSerializer.Serialize(command);
            var key = (command.Player, command.RequestId ?? "");
            if (command.RequestId != null && receipts.TryGetValue(key, out var receipt))
                return receipt.Payload == fingerprint ? receipt.Result : new(false, "Request ID already used for a different command.", state.Version);
            var next = state.Clone();
            try
            {
                Apply(next, command, clock.Milliseconds);
                NormalizeSurrender(next, clock.Milliseconds);
                UpdateAwards(next); CheckVictory(next); AssertInvariants(next);
                next.Version++; state = next;
                var result = new Result(true, "OK", next.Version);
                if (command.RequestId != null) receipts.Add(key, (fingerprint, result));
                return result;
            }
            catch (RuleViolation e) { return new(false, e.Message, state.Version); }
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new RuleViolation(message); }
    private static int Find(State s, string id) => s.Players.FindIndex(p => p.Id == id);
    private static void Current(State s, int p, params Phase[] phases)
    {
        Require(p == s.Active, "It is not your turn."); Require(phases.Contains(s.Phase), "Action is not legal in this phase.");
    }
    private static void Apply(State s, Command c, long now)
    {
        Require(!string.IsNullOrWhiteSpace(c.Player) && c.Player.Length <= 64, "Player ID must contain 1–64 characters.");
        Require(s.Phase is not (Phase.Finished or Phase.Abandoned), "Game is complete.");
        int p = Find(s, c.Player);
        if (c.Kind == CommandKind.AddPlayer)
        {
            Require(s.Phase == Phase.Lobby, "Game has already started."); Require(p < 0, "Player already exists.");
            Require(s.Players.Count < 4, "Game is full."); s.Players.Add(new Player(c.Player)); return;
        }
        Require(p >= 0, "Unknown player.");
        var player = s.Players[p];
        if (c.Kind is CommandKind.Disconnect or CommandKind.Reconnect)
        {
            Require(!player.Surrendered, "Surrender is permanent."); player.Connected = c.Kind == CommandKind.Reconnect;
            if (player.Connected && p == s.Active && s.Phase == Phase.ReconnectGrace) BeginRoll(s, now);
            return;
        }
        Require(!player.Surrendered, "Player has surrendered."); Require(player.Connected, "Player is disconnected.");
        if (c.Kind == CommandKind.Surrender) { Surrender(s, p, now); return; }
        switch (c.Kind)
        {
            case CommandKind.Ready:
                Require(s.Phase == Phase.Lobby, "Ready state is lobby-only."); player.Ready = c.Target != 0; break;
            case CommandKind.Start:
                Require(s.Phase == Phase.Lobby && p == 0, "Only the host may start a lobby.");
                Require(s.Players.Count is >= 2 and <= 4 && s.Players.All(x => x.Ready && x.Connected && !x.Surrendered), "All 2–4 players must be ready and connected.");
                Start(s, now); break;
            case CommandKind.PlaceSettlement:
                Current(s, p, Phase.SetupSettlement); PlaceSettlement(s, p, c.Target, true); s.SetupVertex = c.Target;
                s.Phase = Phase.SetupRoad; s.Deadline = now + 30_000; break;
            case CommandKind.PlaceRoad:
                Current(s, p, Phase.SetupRoad); PlaceRoad(s, p, c.Target, true); NextSetup(s, now); break;
            case CommandKind.Roll:
                Current(s, p, Phase.AwaitRoll); ResolveRoll(s, now); break;
            case CommandKind.BuildRoad:
                Current(s, p, Phase.Action); Pay(s, p, Costs.Road); PlaceRoad(s, p, c.Target, false); break;
            case CommandKind.BuildSettlement:
                Current(s, p, Phase.Action); Pay(s, p, Costs.Settlement); PlaceSettlement(s, p, c.Target, false); break;
            case CommandKind.BuildCity:
                Current(s, p, Phase.Action); Require(s.Buildings.TryGetValue(c.Target, out var b) && b.Owner == p && b.Level == 1, "Upgrade your own settlement.");
                Require(s.Buildings.Values.Count(b => b.Owner == p && b.Level == 2) < 4, "No city pieces left.");
                Pay(s, p, Costs.City); s.Buildings[c.Target] = new(p, 2); break;
            case CommandKind.BuyCard:
                Current(s, p, Phase.Action); Require(s.Deck.Count > 0, "Development deck is empty."); Pay(s, p, Costs.Development);
                player.Cards.Add(new(s.Deck[^1].Kind, s.Turn)); s.Deck.RemoveAt(s.Deck.Count - 1); break;
            case CommandKind.PlayKnight:
                Current(s, p, Phase.AwaitRoll, Phase.Action); Play(s, p, Card.Knight); player.Knights++;
                s.RobberReturn = s.Phase; s.Phase = Phase.Robber; s.Deadline = now + 30_000; break;
            case CommandKind.PlayRoadBuilding:
                Current(s, p, Phase.AwaitRoll, Phase.Action); Play(s, p, Card.RoadBuilding);
                int remaining = 15 - s.Roads.Values.Count(x => x == p);
                Require(remaining > 0, "No road pieces left."); PlaceRoad(s, p, c.Target, false);
                if (c.SecondTarget >= 0) { Require(remaining > 1, "Only one road piece remains."); PlaceRoad(s, p, c.SecondTarget, false); }
                else Require(remaining == 1 || !Enumerable.Range(0, s.Board!.Edges.Length).Any(e => LegalRoad(s, p, e, false)), "Place the second road if legal.");
                break;
            case CommandKind.PlayYearOfPlenty:
                Current(s, p, Phase.AwaitRoll, Phase.Action); Play(s, p, Card.YearOfPlenty);
                int[] take = Bundle(c.Take); Require(take.Sum() == Math.Min(2, s.Bank.Sum()), "Choose two available resources (or the remaining bank supply).");
                TakeBank(s, p, take); break;
            case CommandKind.PlayMonopoly:
                Current(s, p, Phase.AwaitRoll, Phase.Action); Play(s, p, Card.Monopoly); int resource = ResourceIndex(c.Resource);
                foreach (var other in s.Players.Where(x => x != player)) { player.Resources[resource] += other.Resources[resource]; other.Resources[resource] = 0; } break;
            case CommandKind.Discard:
                Require(s.Phase == Phase.Discard && s.Discards.ContainsKey(p), "No discard required.");
                int[] discard = Bundle(c.Give); Require(discard.Sum() == s.Discards[p], "Incorrect discard count."); Pay(s, p, discard); s.Discards.Remove(p);
                if (s.Discards.Count == 0) BeginRobber(s, now); break;
            case CommandKind.MoveRobber:
                Current(s, p, Phase.Robber); MoveRobber(s, c.Target, now); break;
            case CommandKind.ChooseVictim:
                Current(s, p, Phase.Victim); int victim = c.OtherPlayer == null ? c.Target : Find(s, c.OtherPlayer);
                Require(s.Victims.Contains(victim), "Victim is not eligible."); Steal(s, victim); ReturnFromRobber(s, now); break;
            case CommandKind.BankTrade:
                Current(s, p, Phase.Action); int[] give = Bundle(c.Give), receive = Bundle(c.Take);
                Require(give.Count(x => x > 0) == 1 && receive.Count(x => x > 0) == 1, "Trade exactly one resource type for another.");
                int gi = Array.FindIndex(give, x => x > 0), ti = Array.FindIndex(receive, x => x > 0);
                Require(gi != ti && give[gi] == receive[ti] * Ratio(s, p, (Resource)gi), "Incorrect bank/port ratio.");
                Pay(s, p, give); TakeBank(s, p, receive); break;
            case CommandKind.OfferTrade:
                Current(s, p, Phase.Action); int to = Find(s, c.OtherPlayer ?? "");
                Require(to >= 0 && to != p && !s.Players[to].Surrendered && s.Players[to].Connected, "Choose an available trading partner.");
                int[] offer = Bundle(c.Give), want = Bundle(c.Take);
                Require(offer.Sum() > 0 && want.Sum() > 0 && !offer.Where((x, i) => x > 0 && want[i] > 0).Any(), "Trade distinct resource types in both directions.");
                Require(Has(player, offer), "Insufficient offered resources.");
                s.Trades.Add(new(s.NextTrade++, p, to, offer, want)); break;
            case CommandKind.AcceptTrade:
                Require(s.Phase == Phase.Action, "Trading requires the action phase.");
                var trade = s.Trades.FirstOrDefault(t => t.Id == c.Target);
                Require(trade != null && trade.To == p && trade.From == s.Active, "No matching trade offer.");
                Require(!s.Players[trade!.From].Surrendered && s.Players[trade.From].Connected, "Offering player is unavailable.");
                Require(Has(s.Players[trade.From], trade.Give) && Has(player, trade.Take), "Trade resources are no longer available.");
                for (int i = 0; i < 5; i++) { s.Players[trade.From].Resources[i] += trade.Take[i] - trade.Give[i]; player.Resources[i] += trade.Give[i] - trade.Take[i]; }
                s.Trades.Remove(trade); break;
            case CommandKind.CancelTrade:
                var cancel = s.Trades.FirstOrDefault(t => t.Id == c.Target); Require(cancel != null && (cancel.From == p || cancel.To == p), "No matching offer."); s.Trades.Remove(cancel!); break;
            case CommandKind.EndTurn:
                Current(s, p, Phase.Action); NextTurn(s, now); break;
            default: throw new RuleViolation("Unsupported command.");
        }
    }
    private static int[] Bundle(IReadOnlyList<int>? values)
    {
        Require(values != null && values.Count == 5 && values.All(x => x >= 0 && x <= 95), "Resource bundle must have five nonnegative counts, at most 95 each.");
        return values!.ToArray();
    }
    private static int ResourceIndex(Resource? r) { Require(r.HasValue && Enum.IsDefined(r.Value), "Choose a resource."); return (int)r!.Value; }
    private static bool Has(Player p, int[] costs) => costs.Where((n, i) => n > p.Resources[i]).Any() == false;
    private static void Pay(State s, int p, int[] costs)
    {
        Require(Has(s.Players[p], costs), "Insufficient resources.");
        for (int i = 0; i < 5; i++) { s.Players[p].Resources[i] -= costs[i]; s.Bank[i] += costs[i]; }
    }
    private static void TakeBank(State s, int p, int[] amounts)
    {
        Require(!amounts.Where((n, i) => n > s.Bank[i]).Any(), "Bank cannot supply requested resources.");
        for (int i = 0; i < 5; i++) { s.Bank[i] -= amounts[i]; s.Players[p].Resources[i] += amounts[i]; }
    }
    private static void Play(State s, int p, Card kind)
    {
        Require(!s.PlayedCard, "Only one development card may be played per turn.");
        int index = s.Players[p].Cards.FindIndex(c => c.Kind == kind && c.BoughtTurn < s.Turn);
        Require(index >= 0, "No playable card (cards bought this turn cannot be played).");
        s.Players[p].Cards.RemoveAt(index); s.UsedCards.Add(kind); s.PlayedCard = true;
    }
}
public static class Costs
{
    public static int[] Road => [1, 1, 0, 0, 0];
    public static int[] Settlement => [1, 1, 1, 1, 0];
    public static int[] City => [0, 0, 0, 2, 3];
    public static int[] Development => [0, 0, 1, 1, 1];
}
