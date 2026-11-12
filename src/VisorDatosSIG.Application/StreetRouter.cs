namespace VisorDatosSIG.Application;

public readonly record struct MapPoint(double Lat, double Lng)
{
    public bool IsValid => double.IsFinite(Lat) && double.IsFinite(Lng) && Math.Abs(Lat) <= 90 && Math.Abs(Lng) <= 180;

    public double DistanceTo(MapPoint other)
    {
        const double rad = Math.PI / 180;
        var a = Math.Pow(Math.Sin((other.Lat - Lat) * rad / 2), 2)
            + Math.Cos(Lat * rad) * Math.Cos(other.Lat * rad) * Math.Pow(Math.Sin((other.Lng - Lng) * rad / 2), 2);
        return 12_742_000 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}

public sealed record StreetPlan(int[] Stops, MapPoint[] Path, double Meters);

public sealed class StreetRouter
{
    private readonly Dictionary<MapPoint, List<(MapPoint To, double Cost)>> graph = [];
    private readonly ILookup<(int Lat, int Lng), MapPoint> cells;
    private readonly double minLat;
    private readonly double maxLat;
    private const double CellSize = 0.002;
    private const double SnapLimit = 250;

    public StreetRouter(IEnumerable<IReadOnlyList<MapPoint>> lines)
    {
        foreach (var line in lines)
            for (var i = 1; i < line.Count; i++)
            {
                var a = line[i - 1];
                var b = line[i];
                if (a == b) continue;
                var length = a.DistanceTo(b);
                if (!graph.TryGetValue(a, out var from)) graph[a] = from = [];
                if (!graph.TryGetValue(b, out var to)) graph[b] = to = [];
                from.Add((b, length));
                to.Add((a, length));
            }
        cells = graph.Keys.ToLookup(p => (Cell(p.Lat), Cell(p.Lng)));
        if (graph.Count > 0)
        {
            minLat = graph.Keys.Min(p => p.Lat);
            maxLat = graph.Keys.Max(p => p.Lat);
        }
    }

    public StreetPlan Plan(MapPoint start, IEnumerable<(int Id, MapPoint Point)> candidates, int count, CancellationToken token = default)
    {
        if (graph.Count == 0) throw new InvalidOperationException("No hay vías para calcular la ruta.");
        var current = Snap(start) ?? throw new InvalidOperationException("El punto de partida está lejos de la red de vías.");
        var remaining = new List<(int Id, MapPoint Node)>();
        foreach (var candidate in candidates)
        {
            token.ThrowIfCancellationRequested();
            if (Snap(candidate.Point) is { } node) remaining.Add((candidate.Id, node));
        }
        var stops = new List<int>();
        var path = new List<MapPoint> { current };
        double meters = 0;
        while (stops.Count < count && remaining.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var targets = remaining.Select(x => x.Node).ToHashSet();
            var queue = new PriorityQueue<MapPoint, double>();
            var distances = new Dictionary<MapPoint, double> { [current] = 0 };
            var previous = new Dictionary<MapPoint, MapPoint>();
            var found = false;
            queue.Enqueue(current, 0);
            while (queue.TryDequeue(out var node, out var cost))
            {
                if (cost > distances[node]) continue;
                if (targets.Contains(node))
                {
                    var stop = remaining.Where(x => x.Node == node).MinBy(x => x.Id);
                    remaining.Remove(stop);
                    stops.Add(stop.Id);
                    meters += cost;
                    var segment = new Stack<MapPoint>();
                    for (var step = node; step != current; step = previous[step]) segment.Push(step);
                    path.AddRange(segment);
                    current = node;
                    found = true;
                    break;
                }
                foreach (var edge in graph[node])
                {
                    var next = cost + edge.Cost;
                    if (distances.TryGetValue(edge.To, out var old) && next >= old) continue;
                    distances[edge.To] = next;
                    previous[edge.To] = node;
                    queue.Enqueue(edge.To, next);
                }
            }
            if (!found) break;
        }
        return new(stops.ToArray(), path.ToArray(), meters);
    }

    private static int Cell(double degree) => (int)Math.Floor(degree / CellSize);

    private MapPoint? Snap(MapPoint point)
    {
        const double latRange = 0.0023;
        if (!point.IsValid || point.Lat < minLat - latRange || point.Lat > maxLat + latRange) return null;
        var lngRange = Math.Min(360, latRange / Math.Max(0.00001, Math.Cos(point.Lat * Math.PI / 180)));
        var best = SnapLimit;
        MapPoint? nearest = null;
        for (var lat = Cell(point.Lat - latRange); lat <= Cell(point.Lat + latRange); lat++)
            for (var lng = Cell(point.Lng - lngRange); lng <= Cell(point.Lng + lngRange); lng++)
                foreach (var node in cells[(lat, lng)])
                {
                    var distance = node.DistanceTo(point);
                    if (distance > best) continue;
                    best = distance;
                    nearest = node;
                }
        return nearest;
    }
}
