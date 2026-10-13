namespace Game.Contracts;

public enum Resource { Brick, Lumber, Wool, Grain, Ore }
public enum Card { Knight, RoadBuilding, YearOfPlenty, Monopoly, VictoryPoint }
public enum Phase { Lobby, SetupSettlement, SetupRoad, ReconnectGrace, AwaitRoll, Discard, Robber, Victim, Action, Paused, Finished, Abandoned, Handover }
public enum CommandKind { AddPlayer, Ready, Start, PlaceSettlement, PlaceRoad, Roll, BuildRoad, BuildSettlement, BuildCity, BuyCard, PlayKnight, PlayRoadBuilding, PlayYearOfPlenty, PlayMonopoly, Discard, MoveRobber, ChooseVictim, BankTrade, OfferTrade, AcceptTrade, CancelTrade, EndTurn, Disconnect, Reconnect, Surrender, ContinueTurn }

/// <summary>Intent only: dice, prices, scores and trade ratios are never supplied by clients.</summary>
public sealed record Command(CommandKind Kind, string Player, int Target = -1, int SecondTarget = -1,
    Resource? Resource = null, IReadOnlyList<int>? Give = null, IReadOnlyList<int>? Take = null,
    string? OtherPlayer = null, string? RequestId = null, int? FirstDie = null, int? SecondDie = null);
public sealed record Result(bool Success, string Message, long Version);
public sealed record HexView(int Id, int Q, int R, Resource? Resource, int Number, IReadOnlyList<int> Vertices);
public sealed record VertexView(int Id, IReadOnlyList<int> Edges, string? Owner, int Level);
public sealed record EdgeView(int Id, int A, int B, string? Owner);
public sealed record PortView(int A, int B, Resource? Resource);
public sealed record PlayerView(string Id, bool Ready, bool Connected, bool Surrendered, int ResourceCount,
    int DevelopmentCardCount, int PublicPoints, int Knights, int RoadLength,
    IReadOnlyList<int>? Resources, IReadOnlyList<Card>? Cards, int? TotalPoints);
public sealed record TradeView(int Id, string From, string? To, IReadOnlyList<int> Give, IReadOnlyList<int> Take);
public sealed record StartingRollView(int Round, string Player, int First, int Second);
public sealed record DiceRollView(int First, int Second, int Total, IReadOnlyList<int> ResourcesReceived, IReadOnlyDictionary<string,int> PlayerResourceCounts);
public sealed record GameView(Guid GameId, string RoomCode, long Version, Phase Phase, string Host, string? ActivePlayer,
    int Turn, int? LastRoll, int RobberHex, long? DeadlineMilliseconds, string? Winner, string? LongestRoad,
    string? LargestArmy, IReadOnlyList<HexView> Hexes, IReadOnlyList<VertexView> Vertices,
    IReadOnlyList<EdgeView> Edges, IReadOnlyList<PortView> Ports, IReadOnlyList<int> Bank,
    IReadOnlyList<PlayerView> Players, IReadOnlyList<TradeView> Trades,
    IReadOnlyDictionary<string, int> RequiredDiscards, IReadOnlyList<string> EligibleVictims, int DevelopmentDeckCount, long? ActionDeadlineMilliseconds, long? RollDeadlineMilliseconds, Phase? NextPhase, DiceRollView? Dice, IReadOnlyList<StartingRollView> StartingRolls, string? StartingPlayer);
