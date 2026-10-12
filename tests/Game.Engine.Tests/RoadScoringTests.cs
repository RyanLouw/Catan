using Game.Contracts;
using Game.Engine;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class RoadScoringTests
{
    [Fact]
    public void BranchCountsLongestTrailRatherThanAllEdges()
    {
        var e = Graph([(0, 1, 0), (1, 2, 0), (2, 3, 0), (1, 4, 0), (4, 5, 0)]);
        Assert.Equal(4, GameEngine.RoadLength(e.TestState, 0));
    }
    [Fact]
    public void LoopWithTailCanRevisitVertexButCannotReuseEdge()
    {
        var e = Graph([(0, 1, 0), (1, 2, 0), (2, 3, 0), (3, 4, 0), (4, 5, 0), (5, 0, 0), (0, 6, 0), (6, 7, 0)]);
        Assert.Equal(8, GameEngine.RoadLength(e.TestState, 0));
    }
    [Fact]
    public void OpponentSettlementCutsRoadAndOwnSettlementDoesNot()
    {
        var e = Graph([(0, 1, 0), (1, 2, 0), (2, 3, 0), (3, 4, 0), (4, 5, 0), (5, 6, 0)], (3, 1));
        Assert.Equal(3, GameEngine.RoadLength(e.TestState, 0)); e.TestState.Buildings[3] = new(0, 1);
        Assert.Equal(6, GameEngine.RoadLength(e.TestState, 0));
    }
    [Fact]
    public void LoopInterruptedAtOpponentBuildingStillHasSixEdgeTrail()
    {
        var e = Graph([(0, 1, 0), (1, 2, 0), (2, 3, 0), (3, 4, 0), (4, 5, 0), (5, 0, 0)], (0, 1));
        Assert.Equal(6, GameEngine.RoadLength(e.TestState, 0));
    }
    [Fact]
    public void AwardMinimumIncumbentTieTransferAndRemoval()
    {
        var e = Graph([(0, 1, 0), (1, 2, 0), (2, 3, 0), (3, 4, 0), (4, 5, 0),
            (6, 7, 1), (7, 8, 1), (8, 9, 1), (9, 10, 1), (10, 11, 1), (11, 12, 1)]);
        e.TestState.Roads.Remove(10); GameEngine.UpdateAwards(e.TestState); Assert.Equal(-1, e.TestState.Longest);
        e.TestState.Longest = 0; GameEngine.UpdateAwards(e.TestState); Assert.Equal(0, e.TestState.Longest);
        e.TestState.Roads[10] = 1; GameEngine.UpdateAwards(e.TestState); Assert.Equal(1, e.TestState.Longest);
        e.TestState.Buildings[8] = new(0, 1); GameEngine.UpdateAwards(e.TestState); Assert.Equal(0, e.TestState.Longest);
        e.TestState.Buildings[2] = new(1, 1); GameEngine.UpdateAwards(e.TestState); Assert.Equal(-1, e.TestState.Longest);
    }
    [Fact]
    public void LargestArmyMinimumTiesAndTransfer()
    {
        var e = Started(); e.TestState.Players[0].Knights = 2; GameEngine.UpdateAwards(e.TestState); Assert.Equal(-1, e.TestState.Army);
        e.TestState.Players[0].Knights = 3; GameEngine.UpdateAwards(e.TestState); Assert.Equal(0, e.TestState.Army);
        e.TestState.Players[1].Knights = 3; GameEngine.UpdateAwards(e.TestState); Assert.Equal(0, e.TestState.Army);
        e.TestState.Players[1].Knights = 4; GameEngine.UpdateAwards(e.TestState); Assert.Equal(1, e.TestState.Army);
    }
}
