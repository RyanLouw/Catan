using Game.Contracts;
namespace Game.Engine;

internal sealed record HeldCard(Card Kind, int BoughtTurn);
internal sealed class Player(string id)
{
    public string Id = id;
    public bool Ready, Surrendered;
    public bool Connected = true;
    public int[] Resources = new int[5];
    public List<HeldCard> Cards = [];
    public int Knights;
    public Player Clone() => new(Id) { Ready = Ready, Surrendered = Surrendered, Connected = Connected,
        Resources = (int[])Resources.Clone(), Cards = [.. Cards], Knights = Knights };
}
internal sealed record Building(int Owner, int Level);
internal sealed record Trade(int Id, int From, int To, int[] Give, int[] Take);
internal sealed class State
{
    public Guid Id = Guid.NewGuid();
    public string RoomCode = "";
    public long Version;
    public required RandomSource Random;
    public Board? Board;
    public List<Player> Players = [];
    public Phase Phase = Phase.Lobby;
    public GameTiming Timing = GameTiming.Standard;
    public Phase? NextPhase;
    public int Starting, Active, Turn, SetupStep, SetupVertex = -1, Robber, Winner = -1, Longest = -1, Army = -1;
    public int? Roll, FirstDie, SecondDie;
    public Dictionary<int,int[]> Production = [];
    public List<StartingRollView> StartingRolls = [];
    public List<ResourceEventView> ResourceActivity = [];
    public long NextResourceEvent = 1;
    public int[] Bank = [19, 19, 19, 19, 19];
    public Dictionary<int, Building> Buildings = [];
    public Dictionary<int, int> Roads = [];
    public List<HeldCard> Deck = [];
    public List<Trade> Trades = [];
    public int NextTrade = 1;
    public Dictionary<int, int> Discards = [];
    public List<int> Victims = [];
    public bool PlayedCard, Started;
    public List<Card> UsedCards = [];
    public Phase RobberReturn = Phase.Action;
    public long? Deadline, ActionDeadline, RollDeadline;
    public State Clone() => new()
    {
        Id = Id, RoomCode = RoomCode, Version = Version, Random = Random.Clone(), Board = Board,
        Players = Players.Select(p => p.Clone()).ToList(), Phase = Phase, Timing = Timing, NextPhase = NextPhase, Starting = Starting, Active = Active, Turn = Turn,
        SetupStep = SetupStep, SetupVertex = SetupVertex, Robber = Robber, Winner = Winner, Longest = Longest, Army = Army,
        ResourceActivity = ResourceActivity.Select(e=>e with {Received=(int[])e.Received.Clone()}).ToList(), NextResourceEvent = NextResourceEvent,
        Roll = Roll, StartingRolls = [.. StartingRolls], FirstDie = FirstDie, SecondDie = SecondDie, Production = Production.ToDictionary(kv=>kv.Key,kv=>(int[])kv.Value.Clone()), Bank = (int[])Bank.Clone(), Buildings = new(Buildings), Roads = new(Roads), Deck = [.. Deck],
        Trades = Trades.Select(t => t with { Give = (int[])t.Give.Clone(), Take = (int[])t.Take.Clone() }).ToList(),
        NextTrade = NextTrade, Discards = new(Discards), Victims = [.. Victims], PlayedCard = PlayedCard, Started = Started, UsedCards = [.. UsedCards],
        RobberReturn = RobberReturn, Deadline = Deadline, ActionDeadline = ActionDeadline, RollDeadline = RollDeadline
    };
}
internal sealed class RuleViolation(string message) : Exception(message);
