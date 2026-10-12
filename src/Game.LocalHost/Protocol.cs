using Game.Contracts;
using Game.Engine;
namespace Game.LocalHost;

public sealed record Request(string Id, string Op, string? Viewer = null, ulong Seed = 1,
    string[]? Players = null, Command? Command = null);
public sealed record Response(string Id, bool Success, string Message, long ServerMilliseconds,
    GameView? View = null, int[]? LegalRoads = null, int[]? LegalSettlements = null, int[]? TradeRatios = null);

/// <summary>Offline process boundary. Networking/authentication can replace this adapter without changing the engine.</summary>
public sealed class Session(IClock? clock = null)
{
    private readonly IClock clock = clock ?? new SystemClock();
    private GameEngine? engine;
    private readonly object gate = new();
    public Response Handle(Request request)
    {
        lock (gate)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Id)) return Error(request, "A correlation ID is required.");
                if (request.Op == "create")
                {
                    var names = request.Players;
                    if (names == null || names.Length is < 2 or > 4 || names.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 64) || names.Distinct().Count() != names.Length)
                        return Error(request, "Choose 2–4 distinct player names (1–64 characters).");
                    if (!names.Contains(request.Viewer)) return Error(request, "Viewer must participate in this game.");
                    var next = new GameEngine(request.Seed, clock);
                    foreach (string name in names)
                    {
                        var result = next.Execute(new(CommandKind.AddPlayer, name));
                        if (!result.Success) return Error(request, result.Message);
                    }
                    engine = next;
                }
                else if (engine == null) return Error(request, "Create a game first.");
                else if (request.Op is not ("view" or "command")) return Error(request, "Unknown operation.");
                // V1 is a trusted local hot-seat adapter. V2 must authenticate and bind actor/viewer IDs.
                var before = engine!.View(request.Viewer ?? "");
                engine.Tick();
                Result? resultCommand = null;
                if (request.Op == "command")
                {
                    if (request.Command == null) return Error(request, "Command is required.");
                    if (request.Command.Player != request.Viewer) return Error(request, "Selected actor must match the viewer.");
                    resultCommand = engine.Execute(request.Command);
                }
                var view = engine.View(before.Players.Single(p => p.Id == request.Viewer).Id);
                string viewer = request.Viewer!;
                return new(request.Id, resultCommand?.Success ?? true, resultCommand?.Message ?? "OK", clock.Milliseconds, view,
                    engine.LegalRoads(viewer, view.Phase == Phase.SetupRoad).ToArray(),
                    engine.LegalSettlements(viewer, view.Phase == Phase.SetupSettlement).ToArray(),
                    view.Hexes.Count > 0 ? Enum.GetValues<Resource>().Select(r => engine.TradeRatio(viewer, r)).ToArray() : []);
            }
            catch (ArgumentException e) { return Error(request, e.Message); }
        }
    }
    public void Tick() { lock (gate) engine?.Tick(); }
    private Response Error(Request request, string message) => new(request.Id, false, message, clock.Milliseconds);
}
