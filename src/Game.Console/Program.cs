using System.Text.Json;
using Game.Contracts;
using Game.Engine;

if (args.Length > 0 && args[0] == "simulate")
{
    int count = args.Length > 1 ? int.Parse(args[1]) : 1;
    ulong firstSeed = args.Length > 2 ? ulong.Parse(args[2]) : 1;
    int? players = args.Length > 3 ? int.Parse(args[3]) : null;
    if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
    int maxTurns = 0; long totalTurns = 0; var started = System.Diagnostics.Stopwatch.StartNew();
    for (int i = 0; i < count; i++)
    {
        var result = Simulation.Run(firstSeed + (ulong)i, players ?? 2 + i % 3);
        maxTurns = Math.Max(maxTurns, result.Turns); totalTurns += result.Turns;
        if (count <= 10 || (i + 1) % 100 == 0) Console.WriteLine(JsonSerializer.Serialize(result));
    }
    Console.WriteLine($"Completed {count}/{count} games; seeds {firstSeed}–{firstSeed + (ulong)count - 1}; average turns {totalTurns / (double)count:F1}; max turns {maxTurns}; elapsed {started.Elapsed}.");
    return;
}

var clock = new SystemClock();
GameEngine? engine = null; string? selected = null;
var json = new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
Console.WriteLine("Catan Stage 1. Type help. Resource bundles: brick,lumber,wool,grain,ore. IDs are shown by show-board.");
// Host timer runs even while the terminal is waiting for input.
using var timer = new System.Threading.Timer(_ => engine?.Tick(), null, 100, 100);
while (true)
{
    Console.Write("> "); string? line = Console.ReadLine(); if (line == null) break;
    string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (tokens.Length == 0) continue;
    try
    {
        string verb = tokens[0];
        if (verb is "quit" or "exit") break;
        if (verb == "help")
        {
            Console.WriteLine("""
create-game [seed]          add-player NAME          use NAME
ready [true|false]          start-game               show-state
show-board                 legal                    winner
place-settlement VERTEX    place-road EDGE          roll
build-road EDGE            build-settlement VERTEX  build-city VERTEX
buy-card                   knight                   road-building EDGE [EDGE]
year-of-plenty BUNDLE       monopoly RESOURCE        discard BUNDLE
move-robber HEX            choose-victim NAME
bank-trade GIVE TAKE       trade NAME GIVE TAKE     accept-trade ID
cancel-trade ID            end-turn                 disconnect
reconnect                  surrender                quit

Use selects the local actor; the engine validates turn ownership.
Switch actors to make each player's mandatory choices or accept trades.
The host runs real timers while waiting for input. Private views are shown only for the selected actor.
Run seeded complete games: dotnet run --project src/Game.Console -- simulate COUNT FIRST_SEED [PLAYERS]
"""); continue;
        }
        if (verb == "create-game") { engine = new GameEngine(tokens.Length > 1 ? ulong.Parse(tokens[1]) : 1, clock); selected = null; Console.WriteLine("Created isolated game."); continue; }
        if (engine == null) { Console.WriteLine("First create-game."); continue; }
        if (verb == "add-player") { var r = engine.Execute(new(CommandKind.AddPlayer, tokens[1])); if (r.Success) selected ??= tokens[1]; Console.WriteLine(r.Message); continue; }
        if (verb == "use") { _ = engine.View(tokens[1]); selected = tokens[1]; Console.WriteLine($"Selected {selected}."); continue; }
        if (selected == null) { Console.WriteLine("Add a player first."); continue; }
        if (verb == "show-state") { Console.WriteLine(JsonSerializer.Serialize(engine.View(selected), json)); continue; }
        if (verb == "show-board")
        {
            var v = engine.View(selected);
            foreach (var h in v.Hexes) Console.WriteLine($"Hex {h.Id} ({h.Q},{h.R}): {h.Resource?.ToString() ?? "Desert"} {h.Number}; vertices [{string.Join(',', h.Vertices)}]{(h.Id == v.RobberHex ? " ROBBER" : "")}");
            foreach (var edge in v.Edges) Console.WriteLine($"Edge {edge.Id}: {edge.A}–{edge.B} {edge.Owner}");
            foreach (var vertex in v.Vertices.Where(x => x.Level > 0)) Console.WriteLine($"Vertex {vertex.Id}: {vertex.Owner} level {vertex.Level}");
            foreach (var port in v.Ports) Console.WriteLine($"Port {port.A}–{port.B}: {port.Resource?.ToString() ?? "3:1"}");
            continue;
        }
        if (verb == "winner") { Console.WriteLine(engine.View(selected).Winner ?? "No winner yet."); continue; }
        if (verb == "legal")
        {
            var v = engine.View(selected); Console.WriteLine($"Roads [{string.Join(',', engine.LegalRoads(selected, v.Phase == Phase.SetupRoad))}]");
            Console.WriteLine($"Settlements [{string.Join(',', engine.LegalSettlements(selected, v.Phase == Phase.SetupSettlement))}]"); continue;
        }
        int Target() => int.Parse(tokens[1]);
        int[] Bundle(int i) => tokens[i].Split(',').Select(int.Parse).ToArray();
        Command command = verb switch
        {
            "ready" => new(CommandKind.Ready, selected, tokens.Length > 1 && tokens[1] == "false" ? 0 : 1),
            "start-game" => new(CommandKind.Start, selected),
            "place-settlement" => new(CommandKind.PlaceSettlement, selected, Target()),
            "place-road" => new(CommandKind.PlaceRoad, selected, Target()),
            "roll" => new(CommandKind.Roll, selected),
            "build-road" => new(CommandKind.BuildRoad, selected, Target()),
            "build-settlement" => new(CommandKind.BuildSettlement, selected, Target()),
            "build-city" => new(CommandKind.BuildCity, selected, Target()),
            "buy-card" => new(CommandKind.BuyCard, selected),
            "knight" => new(CommandKind.PlayKnight, selected),
            "road-building" => new(CommandKind.PlayRoadBuilding, selected, Target(), tokens.Length > 2 ? int.Parse(tokens[2]) : -1),
            "year-of-plenty" => new(CommandKind.PlayYearOfPlenty, selected, Take: Bundle(1)),
            "monopoly" => new(CommandKind.PlayMonopoly, selected, Resource: Enum.Parse<Resource>(tokens[1], true)),
            "discard" => new(CommandKind.Discard, selected, Give: Bundle(1)),
            "move-robber" => new(CommandKind.MoveRobber, selected, Target()),
            "choose-victim" => new(CommandKind.ChooseVictim, selected, OtherPlayer: tokens[1]),
            "bank-trade" => new(CommandKind.BankTrade, selected, Give: Bundle(1), Take: Bundle(2)),
            "trade" => new(CommandKind.OfferTrade, selected, Give: Bundle(2), Take: Bundle(3), OtherPlayer: tokens[1]),
            "accept-trade" => new(CommandKind.AcceptTrade, selected, Target()),
            "cancel-trade" => new(CommandKind.CancelTrade, selected, Target()),
            "end-turn" => new(CommandKind.EndTurn, selected),
            "disconnect" => new(CommandKind.Disconnect, selected),
            "reconnect" => new(CommandKind.Reconnect, selected),
            "surrender" => new(CommandKind.Surrender, selected),
            _ => throw new ArgumentException("Unknown command; type help.")
        };
        var result = engine.Execute(command); Console.WriteLine($"{(result.Success ? "OK" : "Rejected")}: {result.Message}");
        var status = engine.View(selected); Console.WriteLine($"{status.Phase}; active {status.ActivePlayer}; turn {status.Turn}; winner {status.Winner ?? "none"}");
    }
    catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or IndexOutOfRangeException) { Console.WriteLine($"Invalid input: {e.Message}"); }
}
