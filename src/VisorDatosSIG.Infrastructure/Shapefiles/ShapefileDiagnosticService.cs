using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Dbf;
using NetTopologySuite.IO.Esri.Shapefiles.Readers;
using NetTopologySuite.Simplify;
using System.Text;

namespace VisorDatosSIG.Infrastructure.Shapefiles;

public sealed record OfficialLayer(string Key, string Name, string FileName);

public sealed record DbfFieldInfo(string Name, string Type, int Length, int Scale);

public sealed record ShapefileDiagnostics(
    string Name,
    string ShapefilePath,
    bool HasShp,
    bool HasShx,
    bool HasDbf,
    bool HasPrj,
    int RecordCount,
    string GeometryType,
    Envelope? BoundingBox,
    IReadOnlyList<DbfFieldInfo> Fields,
    bool IsPrjValid,
    string PrjStatus);

public sealed record ShapefileFeature(Geometry Geometry, IReadOnlyDictionary<string, object?> Attributes);

public interface IShapefileDiagnosticService
{
    IReadOnlyList<OfficialLayer> GetOfficialLayers();
    ShapefileDiagnostics GetDiagnostics(string shapefilePath);
    ShapefileDiagnostics GetDiagnosticsForLayer(string layerKey);
    IReadOnlyList<ShapefileFeature> ReadFeaturesFromPath(string shapefilePath, int maximumRecords = 20);
    IReadOnlyList<ShapefileFeature> ReadFeatures(string layerKey, Envelope? bounds = null, double? simplifyTolerance = null, int? maximumRecords = null);
    string GetLayerPath(string layerKey);
}

public sealed class ShapefileDiagnosticService : IShapefileDiagnosticService
{
    // Official polygon rings include records that do not meet strict ESRI orientation rules.
    // This repairs the in-memory geometry only; source files remain untouched.
    private static readonly ShapefileReaderOptions ReaderOptions = new() { GeometryBuilderMode = GeometryBuilderMode.QuickFixInvalidShapes };

    private static readonly IReadOnlyList<OfficialLayer> Layers =
    [
        new("manzanas", "Manzanas", "Exp_MapaBase_MZA_4326.shp"),
        new("lotes", "Lotes", "Exp_MapaBase_LOTES_4326.shp"),
        new("codigos-fijos", "Códigos Fijos", "Exp_CodigoFijo_4326.shp"),
        new("vias", "Vías", "Exp_MapaBase_VIAS_4326.shp")
    ];

    private readonly string _shapefilesDirectory;

    public ShapefileDiagnosticService(string? shapefilesDirectory = null)
    {
        _shapefilesDirectory = shapefilesDirectory ?? FindShapefilesDirectory();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IReadOnlyList<OfficialLayer> GetOfficialLayers() => Layers;

    public string GetLayerPath(string layerKey) => Path.Combine(_shapefilesDirectory, GetLayer(layerKey).FileName);

    public ShapefileDiagnostics GetDiagnosticsForLayer(string layerKey) => GetDiagnostics(GetLayerPath(layerKey));

    public ShapefileDiagnostics GetDiagnostics(string shapefilePath)
    {
        var path = Path.ChangeExtension(shapefilePath, ".shp");
        var hasShp = File.Exists(path);
        var hasShx = File.Exists(Path.ChangeExtension(path, ".shx"));
        var hasDbf = File.Exists(Path.ChangeExtension(path, ".dbf"));
        var prjPath = Path.ChangeExtension(path, ".prj");
        var hasPrj = File.Exists(prjPath);
        var fields = new List<DbfFieldInfo>();
        var recordCount = 0;
        var geometryType = "No disponible";
        Envelope? boundingBox = null;

        if (hasDbf)
        {
            using var dbf = new DbfReader(Path.ChangeExtension(path, ".dbf"));
            recordCount = dbf.RecordCount;
            fields.AddRange(dbf.Fields.Select(field => new DbfFieldInfo(field.Name, field.FieldType.ToString(), field.Length, field.NumericScale)));
        }

        if (hasShp)
        {
            geometryType = Shapefile.GetShapeType(path).ToString();
            foreach (var geometry in Shapefile.ReadAllGeometries(path, ReaderOptions))
            {
                if (geometry is null || geometry.IsEmpty)
                {
                    continue;
                }

                boundingBox ??= new Envelope(geometry.EnvelopeInternal);
                boundingBox.ExpandToInclude(geometry.EnvelopeInternal);
            }
        }

        var prj = hasPrj ? File.ReadAllText(prjPath).Trim() : string.Empty;
        var isPrjValid = prj.Contains("WGS_1984", StringComparison.OrdinalIgnoreCase) || prj.Contains("EPSG", StringComparison.OrdinalIgnoreCase) && prj.Contains("4326", StringComparison.Ordinal);
        var prjStatus = !hasPrj ? "No se encontró el archivo PRJ." : isPrjValid ? "PRJ válido: coordenadas geográficas WGS 84 (EPSG:4326)." : "PRJ presente, pero no se pudo validar EPSG:4326.";

        return new ShapefileDiagnostics(Path.GetFileNameWithoutExtension(path), path, hasShp, hasShx, hasDbf, hasPrj, recordCount, geometryType, boundingBox, fields, isPrjValid, prjStatus);
    }

    public IReadOnlyList<ShapefileFeature> ReadFeatures(string layerKey, Envelope? bounds = null, double? simplifyTolerance = null, int? maximumRecords = null)
    {
        var path = GetLayerPath(layerKey);
        return ReadFeaturesFromFile(path, bounds, simplifyTolerance, maximumRecords);
    }

    public IReadOnlyList<ShapefileFeature> ReadFeaturesFromPath(string shapefilePath, int maximumRecords = 20) =>
        ReadFeaturesFromFile(shapefilePath, null, null, maximumRecords);

    private IReadOnlyList<ShapefileFeature> ReadFeaturesFromFile(string path, Envelope? bounds, double? simplifyTolerance, int? maximumRecords)
    {
        var diagnostics = GetDiagnostics(path);
        EnsureComplete(diagnostics);

        var features = new List<ShapefileFeature>();
        foreach (var feature in Shapefile.ReadAllFeatures(path, ReaderOptions))
        {
            if (feature.Geometry is null || feature.Geometry.IsEmpty || bounds is not null && !feature.Geometry.EnvelopeInternal.Intersects(bounds))
            {
                continue;
            }

            var geometry = simplifyTolerance is > 0 ? DouglasPeuckerSimplifier.Simplify(feature.Geometry, simplifyTolerance.Value) : feature.Geometry;
            var attributes = feature.Attributes.GetNames()
                .Where(IsPublicField)
                .ToDictionary(name => name, name => NormalizeValue(feature.Attributes[name]));
            features.Add(new ShapefileFeature(geometry, attributes));

            if (maximumRecords is not null && features.Count >= maximumRecords.Value)
            {
                break;
            }
        }

        return features;
    }

    private static object? NormalizeValue(object? value) => value switch
    {
        null => null,
        DateTime date => date.ToString("yyyy-MM-dd"),
        _ => value
    };

    private static bool IsPublicField(string fieldName) =>
        !fieldName.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase) &&
        !fieldName.Equals("OID", StringComparison.OrdinalIgnoreCase) &&
        !fieldName.StartsWith("Shape_", StringComparison.OrdinalIgnoreCase);

    private static void EnsureComplete(ShapefileDiagnostics diagnostics)
    {
        if (!diagnostics.HasShp || !diagnostics.HasShx || !diagnostics.HasDbf || !diagnostics.HasPrj)
        {
            throw new InvalidOperationException($"La capa '{diagnostics.Name}' no tiene todos los componentes requeridos (.shp, .shx, .dbf y .prj).");
        }
    }

    private static string FindShapefilesDirectory()
    {
        var configuredPath = Environment.GetEnvironmentVariable("VISORDATOSSIG_SHAPEFILES_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath) && Directory.Exists(configuredPath))
        {
            return configuredPath;
        }

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "data", "shapefiles");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new DirectoryNotFoundException("No se encontró data/shapefiles. Configure VISORDATOSSIG_SHAPEFILES_PATH para indicar su ubicación.");
    }

    private static OfficialLayer GetLayer(string layerKey) => Layers.FirstOrDefault(layer => layer.Key.Equals(layerKey, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"La capa diagnóstica '{layerKey}' no existe.");
}
