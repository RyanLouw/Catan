using Catan.Client;
using Xunit;
namespace Game.Engine.Tests;

public sealed class UnityTransportTests
{
    [Fact]
    public async Task UnityWireAndProcessTransportCanCreateReadyStartAndPreservePrivacy()
    {
        string root = Repository(); string dll = Path.Combine(root, "src/Game.LocalHost/bin/Release/net10.0/Game.LocalHost.dll");
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        using var transport = new LocalProcessTransport(dotnet, "\"" + dll + "\"", root);
        var create = await transport.RequestAsync(new HostRequest { Op = "create", Viewer = "Ryan", Players = ["Ryan", "John"], Seed = 42 });
        Assert.True(create.Success); Assert.Equal("Lobby", create.View.Phase); Assert.NotNull(create.View.Players[0].Resources); Assert.Null(create.View.Players[1].Resources);
        foreach (string p in new[] { "Ryan", "John" })
        {
            var ready = await transport.RequestAsync(new HostRequest { Op = "command", Viewer = p, Command = new Intent { Kind = "Ready", Player = p, Target = 1, RequestId = Guid.NewGuid().ToString() } });
            Assert.True(ready.Success, ready.Message);
        }
        var start = await transport.RequestAsync(new HostRequest { Op = "command", Viewer = "Ryan", Command = new Intent { Kind = "Start", Player = "Ryan", RequestId = "start" } });
        Assert.True(start.Success); Assert.Equal("SetupSettlement", start.View.Phase); Assert.Equal(19, start.View.Hexes.Length); Assert.Equal(72, start.View.Edges.Length);
        Assert.NotEmpty(start.LegalSettlements); Assert.Equal(5, start.TradeRatios.Length);
        var snapshot = await transport.RequestAsync(new HostRequest { Op = "view", Viewer = start.View.ActivePlayer });
        var placed = await transport.RequestAsync(new HostRequest { Op = "command", Viewer = start.View.ActivePlayer,
            Command = new Intent { Kind = "PlaceSettlement", Player = start.View.ActivePlayer, Target = snapshot.LegalSettlements[0], RequestId = "settlement" } });
        Assert.True(placed.Success, placed.Message); Assert.Equal("SetupRoad", placed.View.Phase); Assert.NotEmpty(placed.LegalRoads);
    }
    private static string Repository()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Game.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Cannot locate Game.sln.");
    }
}
