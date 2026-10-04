using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using VisorDatosSIG.Domain.Entities;

namespace VisorDatosSIG.Infrastructure.Persistence;

public sealed record SqlLayerInfo(string Key, string Name, string GeometryType, int Count, double[]? BoundingBox, IReadOnlyList<string> Fields);
public sealed record SqlLayerFeature(int Id, Geometry Geometry, IReadOnlyDictionary<string, object?> Properties);

public interface ISqlLayerService
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SqlLayerInfo>> GetLayersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SqlLayerFeature>> GetFeaturesAsync(string layerKey, Envelope? bbox, int limit, CancellationToken cancellationToken = default);
    Task<SqlLayerFeature?> GetFeatureAsync(string layerKey, int id, CancellationToken cancellationToken = default);
}

public sealed class SqlLayerService(VisorDatosSigDbContext dbContext) : ISqlLayerService
{
    private static readonly IReadOnlyDictionary<string, (string Name, string Type, string[] Fields)> Definitions = new Dictionary<string, (string, string, string[])>(StringComparer.OrdinalIgnoreCase)
    {
        ["manzanas"] = ("Manzanas", "Polygon", ["UV_MZA", "UV", "MZA"]),
        ["lotes"] = ("Lotes", "Polygon", ["NroLote", "IdManzana"]),
        ["codigos-fijos"] = ("Códigos Fijos", "Point", ["CodFijo", "Nombre", "Estado"]),
        ["vias"] = ("Vías", "LineString", ["Nombre", "TipoVia", "OSMID"])
    };

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => await dbContext.Database.CanConnectAsync(cancellationToken);

    public async Task<IReadOnlyList<SqlLayerInfo>> GetLayersAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<SqlLayerInfo>();
        foreach (var definition in Definitions)
        {
            var geometries = await GetGeometries(definition.Key, cancellationToken);
            Envelope? envelope = null;
            foreach (var geometry in geometries.Where(x => x is not null))
            {
                if (envelope is null) envelope = new Envelope(geometry!.EnvelopeInternal);
                else envelope.ExpandToInclude(geometry!.EnvelopeInternal);
            }
            result.Add(new SqlLayerInfo(definition.Key, definition.Value.Name, definition.Value.Type, geometries.Count, envelope is null ? null : [envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY], definition.Value.Fields));
        }
        return result;
    }

    public async Task<IReadOnlyList<SqlLayerFeature>> GetFeaturesAsync(string layerKey, Envelope? bbox, int limit, CancellationToken cancellationToken = default)
    {
        ValidateKey(layerKey);
        limit = Math.Clamp(limit, 1, layerKey.Equals("lotes", StringComparison.OrdinalIgnoreCase) ? 3500 : 10000);
        var bounds = bbox is null ? null : GeometryFactory.Default.ToGeometry(bbox);
        if (bounds is not null) bounds.SRID = 4326;
        return layerKey.ToLowerInvariant() switch
        {
            "manzanas" => (await Filter(dbContext.Manzanas, x => x.Geom, bounds, limit, cancellationToken)).Select(Map).ToList(),
            "lotes" => (await Filter(dbContext.Lotes, x => x.Geom, bounds, limit, cancellationToken)).Select(Map).ToList(),
            "codigos-fijos" => (await Filter(dbContext.CodigosFijos, x => x.Geom, bounds, limit, cancellationToken)).Select(Map).ToList(),
            "vias" => (await Filter(dbContext.Vias, x => x.Geom, bounds, limit, cancellationToken)).Select(Map).ToList(),
            _ => []
        };
    }

    public async Task<SqlLayerFeature?> GetFeatureAsync(string layerKey, int id, CancellationToken cancellationToken = default) => layerKey.ToLowerInvariant() switch
    {
        "manzanas" => (await dbContext.Manzanas.AsNoTracking().FirstOrDefaultAsync(x => x.IdManzana == id, cancellationToken)) is { } item ? Map(item) : null,
        "lotes" => (await dbContext.Lotes.AsNoTracking().FirstOrDefaultAsync(x => x.IdLote == id, cancellationToken)) is { } item ? Map(item) : null,
        "codigos-fijos" => (await dbContext.CodigosFijos.AsNoTracking().FirstOrDefaultAsync(x => x.IdCodigo == id, cancellationToken)) is { } item ? Map(item) : null,
        "vias" => (await dbContext.Vias.AsNoTracking().FirstOrDefaultAsync(x => x.IdVia == id, cancellationToken)) is { } item ? Map(item) : null,
        _ => throw new KeyNotFoundException($"La capa '{layerKey}' no existe.")
    };

    private async Task<List<Geometry?>> GetGeometries(string key, CancellationToken token) => key switch
    {
        "manzanas" => await dbContext.Manzanas.AsNoTracking().Select(x => x.Geom).ToListAsync(token),
        "lotes" => await dbContext.Lotes.AsNoTracking().Select(x => x.Geom).ToListAsync(token),
        "codigos-fijos" => await dbContext.CodigosFijos.AsNoTracking().Select(x => x.Geom).ToListAsync(token),
        "vias" => await dbContext.Vias.AsNoTracking().Select(x => x.Geom).ToListAsync(token),
        _ => throw new KeyNotFoundException($"La capa '{key}' no existe.")
    };

    private static async Task<List<T>> Filter<T>(DbSet<T> set, Func<T, Geometry?> geometry, Geometry? bounds, int limit, CancellationToken token) where T : class =>
        await set.AsNoTracking().Where(x => geometry(x) != null && (bounds == null || geometry(x)!.Intersects(bounds))).Take(limit).ToListAsync(token);

    private static SqlLayerFeature Map(Manzana item) => new(item.IdManzana, item.Geom!, new Dictionary<string, object?> { ["UV_MZA"] = item.UvMza, ["UV"] = item.Uv, ["MZA"] = item.Mza });
    private static SqlLayerFeature Map(Lote item) => new(item.IdLote, item.Geom!, new Dictionary<string, object?> { ["NroLote"] = item.NroLote, ["IdManzana"] = item.IdManzana });
    private static SqlLayerFeature Map(CodigoFijo item) => new(item.IdCodigo, item.Geom!, new Dictionary<string, object?> { ["CodFijo"] = item.CodFijoValor, ["Nombre"] = item.Nombre, ["Estado"] = item.Estado });
    private static SqlLayerFeature Map(Via item) => new(item.IdVia, item.Geom!, new Dictionary<string, object?> { ["Nombre"] = item.Nombre, ["TipoVia"] = item.TipoVia, ["OSMID"] = item.OsmId });
    private static void ValidateKey(string key) { if (!Definitions.ContainsKey(key)) throw new KeyNotFoundException($"La capa '{key}' no existe."); }
}
