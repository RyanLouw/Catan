using Game.Contracts;
using Game.Engine;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;

public sealed class DiceFeedbackTests
{
    [Fact]
    public void ActualDiceFacesSumToResultAndProductionReportMatchesEachPlayersResourceChange()
    {
        bool production=false,seven=false;
        for(ulong seed=1;seed<=50;seed++)
        {
            var e=Started(seed);string active=Active(e);var before=e.TestState.Players.Select(p=>p.Resources.ToArray()).ToArray();OK(e,new(CommandKind.Roll,active));
            for(int p=0;p<e.TestState.Players.Count;p++)
            {
                var view=e.View(e.TestState.Players[p].Id);var dice=view.Dice!;Assert.InRange(dice.First,1,6);Assert.InRange(dice.Second,1,6);Assert.Equal(view.LastRoll,dice.First+dice.Second);Assert.Equal(dice.Total,dice.First+dice.Second);
                int[] received=Enumerable.Range(0,5).Select(r=>e.TestState.Players[p].Resources[r]-before[p][r]).ToArray();Assert.Equal(received,dice.ResourcesReceived);Assert.Equal(received.Sum(),dice.PlayerResourceCounts[e.TestState.Players[p].Id]);
                if(received.Sum()>0)production=true;if(dice.Total==7){seven=true;Assert.Equal(0,received.Sum());}
            }
        }
        Assert.True(production);Assert.True(seven);
    }
    [Fact]
    public void RollReportIsDetachedAndClearsAtNextTurn()
    {
        var e=Started();string p=Active(e);OK(e,new(CommandKind.Roll,p));var view=e.View(p);int expected=view.Dice!.ResourcesReceived[0];((int[])view.Dice.ResourcesReceived)[0]=999;
        Assert.Equal(expected,e.View(p).Dice!.ResourcesReceived[0]);Action(e);OK(e,new(CommandKind.EndTurn,p));Assert.Null(e.View(p).Dice);
    }
    [Fact]
    public void NoBuildingsProduceAnExplicitZeroResourceReport()
    {
        var e=Started();e.TestState.Buildings.Clear();string p=Active(e);OK(e,new(CommandKind.Roll,p));Assert.All(e.View(p).Dice!.ResourcesReceived,n=>Assert.Equal(0,n));Assert.All(e.View(p).Dice!.PlayerResourceCounts.Values,n=>Assert.Equal(0,n));
    }
}
