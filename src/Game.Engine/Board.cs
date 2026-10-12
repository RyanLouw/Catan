using Game.Contracts;
namespace Game.Engine;

internal sealed record Hex(int Id, int Q, int R, Resource? Resource, int Number, int[] Vertices);
internal sealed record Edge(int Id, int A, int B);
internal sealed record Port(int A, int B, Resource? Resource);
internal sealed class Board
{
    public required Hex[] Hexes { get; init; }
    public required Edge[] Edges { get; init; }
    public required int[][] VertexEdges { get; init; }
    public required Port[] Ports { get; init; }
    public int Other(int edge, int vertex) => Edges[edge].A == vertex ? Edges[edge].B : Edges[edge].A;
    public IEnumerable<int> Neighbors(int vertex) => VertexEdges[vertex].Select(e => Other(e, vertex));
    public static Board Generate(RandomSource rng)
    {
        var cells = new List<(int Q, int R)>();
        for (int q = -2; q <= 2; q++) for (int r = -2; r <= 2; r++) if (Math.Abs(q + r) <= 2) cells.Add((q, r));
        var vertices = new Dictionary<(long X, long Y), int>();
        var pairs = new Dictionary<(int A, int B), int>();
        var edges = new List<Edge>(); var counts = new List<int>(); var corners = new List<int[]>();
        foreach (var (q, r) in cells)
        {
            var ids = new int[6];
            for (int k = 0; k < 6; k++)
            {
                double angle = (60 * k - 30) * Math.PI / 180;
                var key = ((long)Math.Round((Math.Sqrt(3) * (q + r / 2.0) + Math.Cos(angle)) * 1e6),
                    (long)Math.Round((1.5 * r + Math.Sin(angle)) * 1e6));
                if (!vertices.TryGetValue(key, out int id)) { id = vertices.Count; vertices.Add(key, id); }
                ids[k] = id;
            }
            corners.Add(ids);
            for (int k = 0; k < 6; k++)
            {
                int a = ids[k], b = ids[(k + 1) % 6]; var key = (Math.Min(a, b), Math.Max(a, b));
                if (!pairs.TryGetValue(key, out int id)) { id = edges.Count; pairs.Add(key, id); edges.Add(new(id, key.Item1, key.Item2)); counts.Add(0); }
                counts[id]++;
            }
        }
        var terrain = new List<Resource?>();
        foreach (Resource resource in Enum.GetValues<Resource>()) terrain.AddRange(Enumerable.Repeat<Resource?>(resource, resource is Resource.Brick or Resource.Ore ? 3 : 4));
        terrain.Add(null); rng.Shuffle(terrain);
        int[] tokens = [2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12];
        int[] numbers;
        do
        {
            rng.Shuffle(tokens); numbers = new int[19]; int cursor = 0;
            for (int i = 0; i < 19; i++) if (terrain[i] != null) numbers[i] = tokens[cursor++];
        } while (Enumerable.Range(0, 19).Any(i => numbers[i] is 6 or 8 && Enumerable.Range(0, i).Any(j => numbers[j] is 6 or 8 && corners[i].Intersect(corners[j]).Count() == 2)));
        var vertexEdges = Enumerable.Range(0, vertices.Count).Select(v => edges.Where(e => e.A == v || e.B == v).Select(e => e.Id).ToArray()).ToArray();
        var coast = edges.Where(e => counts[e.Id] == 1).ToList(); var ordered = new List<Edge>();
        int current = coast[0].A;
        while (coast.Count > 0)
        {
            var e = coast.First(e => e.A == current || e.B == current); ordered.Add(e); coast.Remove(e); current = e.A == current ? e.B : e.A;
        }
        Resource?[] types = [null, null, null, null, Resource.Brick, Resource.Lumber, Resource.Wool, Resource.Grain, Resource.Ore]; rng.Shuffle(types);
        int offset = rng.Next(30);
        var ports = types.Select((t, i) => { var e = ordered[(offset + i * 30 / 9) % 30]; return new Port(e.A, e.B, t); }).ToArray();
        return new Board { Hexes = cells.Select((c, i) => new Hex(i, c.Q, c.R, terrain[i], numbers[i], corners[i])).ToArray(), Edges = edges.ToArray(), VertexEdges = vertexEdges, Ports = ports };
    }
}
