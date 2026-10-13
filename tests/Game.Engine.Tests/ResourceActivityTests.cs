using Game.Contracts;
using Game.Engine;
using Xunit;
using static Game.Engine.Tests.Fixtures;
namespace Game.Engine.Tests;
public sealed class ResourceActivityTests
{
    [Fact]
    public void StartingReceiptsMatchSecondSettlementHexesAndNeverTheFirst()
    {
        var e=Lobby();OK(e,new(CommandKind.Start,"P0"));
        var second=new Dictionary<string,int[]>();
        while(e.TestState.Phase is Phase.SetupSettlement or Phase.SetupRoad)
        {
            string actor=Active(e);
            if(e.TestState.Phase==Phase.SetupSettlement)
            {
                int vertex=e.LegalSettlements(actor,true)[0];bool isSecond=e.TestState.SetupStep>=e.TestState.Players.Count;
                int[] expected=new int[5];
                if(isSecond)foreach(var h in e.TestState.Board!.Hexes.Where(h=>h.Resource.HasValue&&h.Vertices.Contains(vertex)))expected[(int)h.Resource!.Value]++;
                OK(e,new(CommandKind.PlaceSettlement,actor,vertex));
                if(isSecond)
                {
                    second.Add(actor,expected);var receipt=e.View(actor).ResourceActivity.Last();
                    Assert.Equal(actor,receipt.Player);Assert.Equal(vertex,receipt.Vertex);Assert.Equal(expected,receipt.Received);
                }
                else Assert.Empty(e.View(actor).ResourceActivity);
            }
            else OK(e,new(CommandKind.PlaceRoad,actor,e.LegalRoads(actor,true)[0]));
        }
        var view=e.View("P0");Assert.Equal(4,view.ResourceActivity.Count);
        foreach(var p in e.TestState.Players)Assert.Equal(second[p.Id],p.Resources);
        Assert.All(view.ResourceActivity,r=>Assert.Equal("Starting resources",r.Source));e.Validate();
    }
    [Fact]
    public void RollReceiptsArePublicMatchActualDeltasAndSurviveTheNextTurn()
    {
        var e=Started();var before=e.TestState.Players.Select(p=>p.Resources.ToArray()).ToArray();string actor=Active(e);
        var command=new Command(CommandKind.Roll,actor,RequestId:"roll");OK(e,command);
        var view=e.View(actor);var receipts=view.ResourceActivity.Where(r=>r.Source=="Dice roll").ToArray();Assert.Equal(4,receipts.Length);
        for(int p=0;p<4;p++)
        {
            var received=receipts.Single(r=>r.Player==e.TestState.Players[p].Id);
            Assert.Equal(view.LastRoll,received.Roll);
            Assert.Equal(Enumerable.Range(0,5).Select(r=>e.TestState.Players[p].Resources[r]-before[p][r]).ToArray(),received.Received);
            Assert.Equal(received.Received,e.View("P1").ResourceActivity.Single(r=>r.Id==received.Id).Received);
        }
        OK(e,command);Assert.Equal(view.ResourceActivity.Count,e.View(actor).ResourceActivity.Count);
        if(e.TestState.Phase==Phase.Action){OK(e,new(CommandKind.EndTurn,actor));Assert.Equal(view.ResourceActivity.Count,e.View(actor).ResourceActivity.Count);}
        Assert.Null(e.View("P0").Players[1].Resources);
    }
    [Fact]
    public void ReceiptsCannotMutateLiveStateOrClonedTransactions()
    {
        var e=Started();var before=Snapshot(e);var clone=e.TestState.Clone();
        clone.ResourceActivity[0].Received[0]=999;((int[])e.View("P0").ResourceActivity[0].Received)[0]=999;
        Assert.Equal(before,Snapshot(e));e.Validate();
    }
}
