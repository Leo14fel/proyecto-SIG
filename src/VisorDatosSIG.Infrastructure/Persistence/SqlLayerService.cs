using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using VisorDatosSIG.Domain.Entities;

namespace VisorDatosSIG.Infrastructure.Persistence;

public sealed record SqlLayerInfo(string Key, string Name, string GeometryType, int Count, double[]? BoundingBox, IReadOnlyList<string> Fields);
public sealed record SqlLayerFeature(int Id, Geometry Geometry, IReadOnlyDictionary<string, object?> Properties);
public sealed record SqlSearchResult(string LayerKey, int Id, IReadOnlyDictionary<string, object?> Properties);
public sealed record SqlSearchPage(int Total, IReadOnlyList<SqlSearchResult> Items);

public interface ISqlLayerService
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SqlLayerInfo>> GetLayersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SqlLayerFeature>> GetFeaturesAsync(string layerKey, Envelope? bbox, int limit, CancellationToken cancellationToken = default);
    Task<SqlLayerFeature?> GetFeatureAsync(string layerKey, int id, CancellationToken cancellationToken = default);
    Task<SqlSearchPage> SearchAsync(string layerKey, string query, int page, int pageSize, string? field = null, string? comparison = null, CancellationToken cancellationToken = default);
}

public sealed class SqlLayerService(VisorDatosSigDbContext dbContext) : ISqlLayerService
{
    private static readonly IReadOnlyDictionary<string, (string Name, string Type, string[] Fields)> Definitions = new Dictionary<string, (string, string, string[])>(StringComparer.OrdinalIgnoreCase)
    {
        ["manzanas"] = ("Manzanas", "Polygon", ["IdManzana", "IdOrigen", "UV_MZA", "UV", "MZA"]),
        ["lotes"] = ("Lotes", "Polygon", ["IdLote", "IdOrigen", "NroLote", "IdManzana"]),
        ["codigos-fijos"] = ("Códigos Fijos", "Point", ["IdCodigo", "CodF_SQL", "CodF_SIG", "CodFijo", "Nombre", "Estado", "FechaCambioEstado", "IdLote", "Longitud", "Latitud"]),
        ["vias"] = ("Vías", "LineString", ["IdVia", "OBJECTID", "Nombre", "TipoVia", "OSMID"])
    };

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => await dbContext.Database.CanConnectAsync(cancellationToken);

    public async Task<IReadOnlyList<SqlLayerInfo>> GetLayersAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<SqlLayerInfo>();
        foreach (var definition in Definitions)
        {
            var geometries = await GetGeometriesAsync(definition.Key, cancellationToken);
            Envelope? envelope = null;
            foreach (var geometry in geometries.Where(geometry => geometry is not null))
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

        // Keep predicates inside each IQueryable: invoking a Func<T, Geometry> is not translatable by EF Core.
        return layerKey.ToLowerInvariant() switch
        {
            "manzanas" => (await dbContext.Manzanas.AsNoTracking().Where(x => x.Geom != null && (bounds == null || x.Geom.Intersects(bounds))).OrderBy(x => x.IdManzana).Take(limit).ToListAsync(cancellationToken)).Select(Map).ToList(),
            "lotes" => (await dbContext.Lotes.AsNoTracking().Where(x => x.Geom != null && (bounds == null || x.Geom.Intersects(bounds))).OrderBy(x => x.IdLote).Take(limit).ToListAsync(cancellationToken)).Select(Map).ToList(),
            "codigos-fijos" => (await dbContext.CodigosFijos.AsNoTracking().Where(x => x.Geom != null && (bounds == null || x.Geom.Intersects(bounds))).OrderBy(x => x.IdCodigo).Take(limit).ToListAsync(cancellationToken)).Select(Map).ToList(),
            "vias" => (await dbContext.Vias.AsNoTracking().Where(x => x.Geom != null && (bounds == null || x.Geom.Intersects(bounds))).OrderBy(x => x.IdVia).Take(limit).ToListAsync(cancellationToken)).Select(Map).ToList(),
            _ => []
        };
    }

    public async Task<SqlLayerFeature?> GetFeatureAsync(string layerKey, int id, CancellationToken cancellationToken = default) => layerKey.ToLowerInvariant() switch
    {
        "manzanas" => (await dbContext.Manzanas.AsNoTracking().FirstOrDefaultAsync(x => x.IdManzana == id && x.Geom != null, cancellationToken)) is { } item ? Map(item) : null,
        "lotes" => (await dbContext.Lotes.AsNoTracking().FirstOrDefaultAsync(x => x.IdLote == id && x.Geom != null, cancellationToken)) is { } item ? Map(item) : null,
        "codigos-fijos" => (await dbContext.CodigosFijos.AsNoTracking().FirstOrDefaultAsync(x => x.IdCodigo == id && x.Geom != null, cancellationToken)) is { } item ? Map(item) : null,
        "vias" => (await dbContext.Vias.AsNoTracking().FirstOrDefaultAsync(x => x.IdVia == id && x.Geom != null, cancellationToken)) is { } item ? Map(item) : null,
        _ => throw new KeyNotFoundException($"La capa '{layerKey}' no existe.")
    };

    public async Task<SqlSearchPage> SearchAsync(string layerKey, string query, int page, int pageSize, string? field = null, string? comparison = null, CancellationToken cancellationToken = default)
    {
        ValidateKey(layerKey);
        query = query.Trim().ToLowerInvariant();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;
        var numericQuery = int.TryParse(query, out var number) ? number : (int?)null;

        return layerKey.ToLowerInvariant() switch
        {
            "manzanas" => await SearchManzanasAsync(query, numericQuery, skip, pageSize, field, comparison, cancellationToken),
            "lotes" => await SearchLotesAsync(query, numericQuery, skip, pageSize, field, comparison, cancellationToken),
            "codigos-fijos" => await SearchCodigosFijosAsync(query, numericQuery, skip, pageSize, field, comparison, cancellationToken),
            "vias" => await SearchViasAsync(query, numericQuery, skip, pageSize, field, comparison, cancellationToken),
            _ => throw new KeyNotFoundException($"La capa '{layerKey}' no existe.")
        };
    }

    private async Task<SqlSearchPage> SearchManzanasAsync(string query, int? number, int skip, int take, string? field, string? comparison, CancellationToken token)
    {
        var source = dbContext.Manzanas.AsNoTracking();
        source = field switch
        {
            null or "" => source.Where(x => x.UvMza != null && x.UvMza.ToLower().Contains(query) || x.Uv != null && x.Uv.ToLower().Contains(query) || x.Mza != null && x.Mza.ToLower().Contains(query) || number != null && (x.IdManzana == number || x.IdOrigen == number)),
            "UV_MZA" => Filter(source, x => x.UvMza, query, comparison),
            "UV" => Filter(source, x => x.Uv, query, comparison),
            "MZA" => Filter(source, x => x.Mza, query, comparison),
            _ => throw new ArgumentException("El campo no está permitido para Manzanas.")
        };
        return new SqlSearchPage(await source.CountAsync(token), (await source.OrderBy(x => x.IdManzana).Skip(skip).Take(take).ToListAsync(token)).Select(x => new SqlSearchResult("manzanas", x.IdManzana, Map(x).Properties)).ToList());
    }

    private async Task<SqlSearchPage> SearchLotesAsync(string query, int? number, int skip, int take, string? field, string? comparison, CancellationToken token)
    {
        var source = dbContext.Lotes.AsNoTracking();
        source = field switch
        {
            null or "" => source.Where(x => x.NroLote != null && x.NroLote.ToLower().Contains(query) || number != null && (x.IdLote == number || x.IdOrigen == number || x.IdManzana == number)),
            "NroLote" => Filter(source, x => x.NroLote, query, comparison),
            _ => throw new ArgumentException("El campo no está permitido para Lotes.")
        };
        return new SqlSearchPage(await source.CountAsync(token), (await source.OrderBy(x => x.IdLote).Skip(skip).Take(take).ToListAsync(token)).Select(x => new SqlSearchResult("lotes", x.IdLote, Map(x).Properties)).ToList());
    }

    private async Task<SqlSearchPage> SearchCodigosFijosAsync(string query, int? number, int skip, int take, string? field, string? comparison, CancellationToken token)
    {
        var source = dbContext.CodigosFijos.AsNoTracking();
        source = field switch
        {
            null or "" => source.Where(x => x.CodFSig != null && x.CodFSig.ToLower().Contains(query) || x.Nombre != null && x.Nombre.ToLower().Contains(query) || number != null && (x.IdCodigo == number || x.CodFSql == number || x.CodFijoValor == number || x.Estado == number || x.IdLote == number)),
            "CodF_SIG" => Filter(source, x => x.CodFSig, query, comparison),
            "Nombre" => Filter(source, x => x.Nombre, query, comparison),
            "CodFijo" when number is not null && comparison is "igual" => source.Where(x => x.CodFijoValor == number),
            "CodFijo" => throw new ArgumentException("CodFijo requiere un valor numérico y el operador Igual."),
            _ => throw new ArgumentException("El campo no está permitido para Códigos Fijos.")
        };
        return new SqlSearchPage(await source.CountAsync(token), (await source.OrderBy(x => x.IdCodigo).Skip(skip).Take(take).ToListAsync(token)).Select(x => new SqlSearchResult("codigos-fijos", x.IdCodigo, Map(x).Properties)).ToList());
    }

    private async Task<SqlSearchPage> SearchViasAsync(string query, int? number, int skip, int take, string? field, string? comparison, CancellationToken token)
    {
        var source = dbContext.Vias.AsNoTracking();
        source = field switch
        {
            null or "" => source.Where(x => x.Nombre != null && x.Nombre.ToLower().Contains(query) || x.TipoVia != null && x.TipoVia.ToLower().Contains(query) || x.OsmId != null && x.OsmId.ToLower().Contains(query) || number != null && (x.IdVia == number || x.ObjectId == number)),
            "Nombre" => Filter(source, x => x.Nombre, query, comparison),
            "TipoVia" => Filter(source, x => x.TipoVia, query, comparison),
            "OSMID" => Filter(source, x => x.OsmId, query, comparison),
            _ => throw new ArgumentException("El campo no está permitido para Vías.")
        };
        return new SqlSearchPage(await source.CountAsync(token), (await source.OrderBy(x => x.IdVia).Skip(skip).Take(take).ToListAsync(token)).Select(x => new SqlSearchResult("vias", x.IdVia, Map(x).Properties)).ToList());
    }

    private static IQueryable<T> Filter<T>(IQueryable<T> source, System.Linq.Expressions.Expression<Func<T, string?>> selector, string value, string? comparison) => comparison switch
    {
        "igual" => source.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(System.Linq.Expressions.Expression.Equal(selector.Body, System.Linq.Expressions.Expression.Constant(value, typeof(string))), selector.Parameters)),
        "comienza-con" => source.Where(BuildStringCall(selector, nameof(string.StartsWith), value)),
        "contiene" => source.Where(BuildStringCall(selector, nameof(string.Contains), value)),
        _ => throw new ArgumentException("El operador no está permitido.")
    };

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildStringCall<T>(System.Linq.Expressions.Expression<Func<T, string?>> selector, string method, string value)
    {
        var notNull = System.Linq.Expressions.Expression.NotEqual(selector.Body, System.Linq.Expressions.Expression.Constant(null, typeof(string)));
        var call = System.Linq.Expressions.Expression.Call(selector.Body, method, Type.EmptyTypes, System.Linq.Expressions.Expression.Constant(value));
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(System.Linq.Expressions.Expression.AndAlso(notNull, call), selector.Parameters);
    }

    private async Task<List<Geometry?>> GetGeometriesAsync(string key, CancellationToken token) => key switch
    {
        "manzanas" => await dbContext.Manzanas.AsNoTracking().Select(x => x.Geom).ToListAsync(token),
        "lotes" => await dbContext.Lotes.AsNoTracking().Select(x => x.Geom).ToListAsync(token),
        "codigos-fijos" => await dbContext.CodigosFijos.AsNoTracking().Select(x => x.Geom).ToListAsync(token),
        "vias" => await dbContext.Vias.AsNoTracking().Select(x => x.Geom).ToListAsync(token),
        _ => throw new KeyNotFoundException($"La capa '{key}' no existe.")
    };

    private static SqlLayerFeature Map(Manzana item) => new(item.IdManzana, item.Geom!, new Dictionary<string, object?> { ["IdManzana"] = item.IdManzana, ["IdOrigen"] = item.IdOrigen, ["UV_MZA"] = item.UvMza, ["UV"] = item.Uv, ["MZA"] = item.Mza });
    private static SqlLayerFeature Map(Lote item) => new(item.IdLote, item.Geom!, new Dictionary<string, object?> { ["IdLote"] = item.IdLote, ["IdOrigen"] = item.IdOrigen, ["NroLote"] = item.NroLote, ["IdManzana"] = item.IdManzana });
    private static SqlLayerFeature Map(CodigoFijo item) => new(item.IdCodigo, item.Geom!, new Dictionary<string, object?> { ["IdCodigo"] = item.IdCodigo, ["CodF_SQL"] = item.CodFSql, ["CodF_SIG"] = item.CodFSig, ["CodFijo"] = item.CodFijoValor, ["Nombre"] = item.Nombre, ["Estado"] = item.Estado, ["FechaCambioEstado"] = item.FechaCambioEstado, ["IdLote"] = item.IdLote, ["Longitud"] = item.Longitud, ["Latitud"] = item.Latitud });
    private static SqlLayerFeature Map(Via item) => new(item.IdVia, item.Geom!, new Dictionary<string, object?> { ["IdVia"] = item.IdVia, ["OBJECTID"] = item.ObjectId, ["Nombre"] = item.Nombre, ["TipoVia"] = item.TipoVia, ["OSMID"] = item.OsmId });
    private static void ValidateKey(string key) { if (!Definitions.ContainsKey(key)) throw new KeyNotFoundException($"La capa '{key}' no existe."); }
}
