#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Catan.Client
{
    // Wire models deliberately use Unity-compatible C# and no .NET 10 assembly references.
    [Serializable] public sealed class Intent
    {
        public string Kind, Player, OtherPlayer, Resource, RequestId;
        public int Target = -1, SecondTarget = -1;
        public int[] Give, Take;
        public int? FirstDie, SecondDie;
    }
    [Serializable] public sealed class HostRequest
    {
        public string Id, Op, Viewer;
        public ulong Seed = 1;
        public bool RelaxedTimers, Testing;
        public string[] Players;
        public Intent Command;
    }
    [Serializable] public sealed class HostResponse
    {
        public string Id, Message;
        public int ProtocolVersion;
        public bool Success, Testing;
        public Dictionary<string,int[]> TableResources;
        public long ServerMilliseconds;
        public Snapshot View;
        public int[] LegalRoads, LegalSettlements, TradeRatios, LegalCities;
        public Dictionary<string, int[]> Costs;
        public TimingProfile Timing;
    }
    [Serializable] public sealed class Snapshot
    {
        public string GameId, RoomCode, Phase, Host, ActivePlayer, Winner, LongestRoad, LargestArmy, NextPhase;
        public long Version;
        public int Turn, RobberHex, DevelopmentDeckCount;
        public int? LastRoll;
        public DiceResult Dice;
        public StartingRoll[] StartingRolls;
        public ResourceEvent[] ResourceActivity;
        public string StartingPlayer;
        public long? DeadlineMilliseconds, ActionDeadlineMilliseconds, RollDeadlineMilliseconds;
        public Terrain[] Hexes;
        public Intersection[] Vertices;
        public Road[] Edges;
        public Harbor[] Ports;
        public Participant[] Players;
        public Offer[] Trades;
        public int[] Bank;
        public Dictionary<string, int> RequiredDiscards;
        public string[] EligibleVictims;
    }
    [Serializable] public sealed class ResourceEvent
    {
        public long Id;
        public string Player, Source;
        public int[] Received;
        public int? Roll, Vertex;
    }
    [Serializable] public sealed class StartingRoll
    {
        public int Round, First, Second;
        public string Player;
    }
    [Serializable] public sealed class DiceResult
    {
        public int First, Second, Total;
        public int[] ResourcesReceived;
        public Dictionary<string,int> PlayerResourceCounts;
    }
    [Serializable] public sealed class TimingProfile
    {
        public long RollMilliseconds, MandatoryMilliseconds, ActionMilliseconds, HandoverMilliseconds;
    }
    [Serializable] public sealed class Terrain { public int Id, Q, R, Number; public string Resource; public int[] Vertices; }
    [Serializable] public sealed class Intersection { public int Id, Level; public int[] Edges; public string Owner; }
    [Serializable] public sealed class Road { public int Id, A, B; public string Owner; }
    [Serializable] public sealed class Harbor { public int A, B; public string Resource; }
    [Serializable] public sealed class Participant
    {
        public string Id;
        public bool Ready, Connected, Surrendered;
        public int ResourceCount, DevelopmentCardCount, PublicPoints, Knights, RoadLength;
        public int? TotalPoints;
        public int[] Resources;
        public string[] Cards;
    }
    [Serializable] public sealed class Offer { public int Id; public string From, To; public int[] Give, Take;
        public int? FirstDie, SecondDie; }
    public interface IGameTransport : IDisposable { Task<HostResponse> RequestAsync(HostRequest request); }
}
