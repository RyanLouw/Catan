using Game.Contracts;
using Game.Engine;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class DevelopmentAndVictoryTests
{
    [Fact]
    public void BuyingCardHasExactCostAndNewCardsCannotBePlayed()
    {
        var e = Started(); Action(e); int p = e.TestState.Active; string id = Active(e); Resources(e, p, 0, 0, 1, 1, 1);
        int deck = e.TestState.Deck.Count; OK(e, new(CommandKind.BuyCard, id)); Assert.Equal(deck - 1, e.TestState.Deck.Count);
        Assert.Single(e.TestState.Players[p].Cards); Assert.Equal(new int[5], e.TestState.Players[p].Resources);
        GrantCard(e, p, Card.Knight, true); RejectedUnchanged(e, new(CommandKind.PlayKnight, id));
    }
    [Fact]
    public void KnightCanBePlayedBeforeRollAndOnlyOneActionCardPerTurn()
    {
        var e = Started(); int p = e.TestState.Active; string id = Active(e); GrantCard(e, p, Card.Knight); GrantCard(e, p, Card.Monopoly);
        OK(e, new(CommandKind.PlayKnight, id)); Assert.Equal(Phase.Robber, e.TestState.Phase); Assert.Equal(1, e.TestState.Players[p].Knights);
        int target = e.TestState.Board!.Hexes.First(h => h.Id != e.TestState.Robber && h.Vertices.All(v => !e.TestState.Buildings.ContainsKey(v))).Id;
        OK(e, new(CommandKind.MoveRobber, id, target)); Assert.Equal(Phase.AwaitRoll, e.TestState.Phase);
        RejectedUnchanged(e, new(CommandKind.PlayMonopoly, id, Resource: Resource.Brick));
    }
    [Fact]
    public void MonopolyTakesChosenResourceFromAllOthersIncludingSurrenderedPlayers()
    {
        var e = Started(); Action(e, 0); GrantCard(e, 0, Card.Monopoly);
        Resources(e, 0, 0, 0, 0, 0, 0); Resources(e, 1, 2, 1, 0, 0, 0); Resources(e, 2, 3, 0, 0, 0, 0); Resources(e, 3, 1, 0, 0, 0, 0);
        e.TestState.Players[2].Surrendered = true;
        OK(e, new(CommandKind.PlayMonopoly, "P0", Resource: Resource.Brick)); Assert.Equal(6, e.TestState.Players[0].Resources[0]);
        Assert.All(e.TestState.Players.Skip(1), p => Assert.Equal(0, p.Resources[0])); Assert.Equal(1, e.TestState.Players[1].Resources[1]);
    }
    [Fact]
    public void YearOfPlentyUsesFiniteBankAndInvalidChoiceDoesNotConsumeCard()
    {
        var e = Started(); Action(e); int p = e.TestState.Active; string id = Active(e); GrantCard(e, p, Card.YearOfPlenty);
        RejectedUnchanged(e, new(CommandKind.PlayYearOfPlenty, id, Take: [3, 0, 0, 0, 0]));
        int brick = e.TestState.Players[p].Resources[0]; int ore = e.TestState.Players[p].Resources[4];
        OK(e, new(CommandKind.PlayYearOfPlenty, id, Take: [1, 0, 0, 0, 1]));
        Assert.Equal(brick + 1, e.TestState.Players[p].Resources[0]); Assert.Equal(ore + 1, e.TestState.Players[p].Resources[4]);
    }
    [Fact]
    public void RoadBuildingPlacesTwoFreeRoadsAtomically()
    {
        var e = Started(); Action(e); int p = e.TestState.Active; string id = Active(e); GrantCard(e, p, Card.RoadBuilding);
        int[] roads = e.LegalRoads(id).Take(2).ToArray(); string before = Snapshot(e);
        RejectedUnchanged(e, new(CommandKind.PlayRoadBuilding, id, roads[0], roads[0])); Assert.Equal(before, Snapshot(e));
        int[] resources = e.TestState.Players[p].Resources.ToArray(); int count = e.TestState.Roads.Count;
        OK(e, new(CommandKind.PlayRoadBuilding, id, roads[0], roads[1])); Assert.Equal(count + 2, e.TestState.Roads.Count); Assert.Equal(resources, e.TestState.Players[p].Resources);
    }
    [Fact]
    public void DevelopmentCardsRejectWrongPlayerWrongPhaseUnavailableCardAndInvalidResource()
    {
        var e = Started(); Action(e); int p = e.TestState.Active; string id = Active(e); GrantCard(e, p, Card.Monopoly);
        RejectedUnchanged(e, new(CommandKind.PlayMonopoly, id, Resource: (Resource)100));
        RejectedUnchanged(e, new(CommandKind.PlayKnight, id)); RejectedUnchanged(e, new(CommandKind.PlayMonopoly, e.TestState.Players[(p + 1) % 4].Id, Resource: Resource.Ore));
        e.TestState.Phase = Phase.Discard; RejectedUnchanged(e, new(CommandKind.PlayMonopoly, id, Resource: Resource.Ore));
    }
    [Fact]
    public void VictoryIsOnlyOnOwnTurnAndFinishedGameRejectsAllCommands()
    {
        var e = Started(); Action(e, 0);
        // Score 10: five VP cards, two starting settlements upgraded to cities, and one extra settlement.
        for (int i = 0; i < 5; i++) GrantCard(e, 1, Card.VictoryPoint);
        foreach (int v in e.TestState.Buildings.Where(kv => kv.Value.Owner == 1).Select(kv => kv.Key).ToArray()) e.TestState.Buildings[v] = new(1, 2);
        int extra = e.LegalSettlements("P1", true)[0]; e.TestState.Buildings.Add(extra, new(1, 1));
        Assert.Equal(10, e.View("P1").Players[1].TotalPoints); Assert.Equal(5, e.View("P0").Players[1].PublicPoints);
        Resources(e, 0, 0, 0, 1, 0, 0);
        OK(e, new(CommandKind.OfferTrade, "P0", Give: [0, 0, 1, 0, 0], Take: [1, 0, 0, 0, 0], OtherPlayer: "P1"));
        Assert.Null(e.View("P0").Winner); OK(e, new(CommandKind.EndTurn, "P0"));
        Assert.Equal(Phase.Finished, e.TestState.Phase); Assert.Equal("P1", e.View("P0").Winner);
        foreach (CommandKind kind in Enum.GetValues<CommandKind>()) RejectedUnchanged(e, new(kind, "P1", 0));
    }
    [Fact]
    public void SurrenderedPlayerCannotWin()
    {
        var e = Started(); Action(e, 0); for (int i = 0; i < 5; i++) GrantCard(e, 1, Card.VictoryPoint);
        foreach (int v in e.TestState.Buildings.Where(kv => kv.Value.Owner == 1).Select(kv => kv.Key).ToArray()) e.TestState.Buildings[v] = new(1, 2);
        e.TestState.Buildings.Add(e.LegalSettlements("P1", true)[0], new(1, 1)); OK(e, new(CommandKind.Surrender, "P1")); OK(e, new(CommandKind.EndTurn, "P0"));
        Assert.NotEqual(Phase.Finished, e.TestState.Phase); Assert.Equal(2, e.TestState.Active);
    }
}
