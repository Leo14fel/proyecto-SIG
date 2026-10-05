using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using VisorDatosSIG.Domain.Entities;
using VisorDatosSIG.Infrastructure.Persistence;

namespace VisorDatosSIG.Infrastructure.Shapefiles;

public enum ShapefileMigrationMode { Replace, Append }

public sealed record ShapefileLayerMigrationResult(string Layer, int SourceRecords, int Inserted, int SkippedEmpty, int SkippedInvalid, int DatabaseRecords);
public sealed record ShapefileMigrationProgress(string Layer, int Processed, int Total, int Succeeded, int Skipped, int Failed, TimeSpan Elapsed);

public sealed record ShapefileMigrationValidation(bool IsValid, ShapefileMigrationMode Mode, IReadOnlyList<ShapefileLayerMigrationResult> Layers, IReadOnlyList<string> Errors);

public interface IShapefileSqlMigrationService
{
    Task<ShapefileMigrationValidation> ValidateAsync(ShapefileMigrationMode mode, CancellationToken cancellationToken = default);
    Task<ShapefileMigrationValidation> MigrateAsync(ShapefileMigrationMode mode, IProgress<ShapefileMigrationProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class ShapefileSqlMigrationService(VisorDatosSigDbContext dbContext, IShapefileDiagnosticService shapefiles) : IShapefileSqlMigrationService
{
    private static readonly string[] LayerKeys = ["manzanas", "lotes", "codigos-fijos", "vias"];

    public async Task<ShapefileMigrationValidation> ValidateAsync(ShapefileMigrationMode mode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        var results = new List<ShapefileLayerMigrationResult>();
        foreach (var layer in LayerKeys)
        {
            var diagnostics = shapefiles.GetDiagnosticsForLayer(layer);
            if (!diagnostics.HasShp || !diagnostics.HasShx || !diagnostics.HasDbf || !diagnostics.HasPrj || !diagnostics.IsPrjValid)
            {
                errors.Add($"{layer}: se requieren SHP, SHX, DBF y PRJ EPSG:4326 válidos.");
                results.Add(new ShapefileLayerMigrationResult(layer, diagnostics.RecordCount, 0, 0, 0, 0));
                continue;
            }

            var features = shapefiles.ReadFeatures(layer, maximumRecords: null, includeSystemFields: true, includeEmpty: true);
            var empty = features.Count(feature => feature.Geometry is null || feature.Geometry.IsEmpty);
            var invalid = features.Count(feature => feature.Geometry is not null && !feature.Geometry.IsEmpty && !feature.Geometry.IsValid);
            results.Add(new ShapefileLayerMigrationResult(layer, diagnostics.RecordCount, 0, empty, invalid, await CountRecordsAsync(layer, cancellationToken)));
        }

        return new ShapefileMigrationValidation(errors.Count == 0, mode, results, errors);
    }

    public async Task<ShapefileMigrationValidation> MigrateAsync(ShapefileMigrationMode mode, IProgress<ShapefileMigrationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(mode, cancellationToken);
        if (!validation.IsValid)
        {
            return validation;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (mode == ShapefileMigrationMode.Replace)
            {
                await dbContext.CodigosFijos.ExecuteDeleteAsync(cancellationToken);
                await dbContext.Lotes.ExecuteDeleteAsync(cancellationToken);
                await dbContext.Manzanas.ExecuteDeleteAsync(cancellationToken);
                await dbContext.Vias.ExecuteDeleteAsync(cancellationToken);
            }

            var results = new List<ShapefileLayerMigrationResult>();
            foreach (var layer in LayerKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await ImportLayerAsync(layer, progress, cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
            return new ShapefileMigrationValidation(true, mode, results, []);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<ShapefileLayerMigrationResult> ImportLayerAsync(string layer, IProgress<ShapefileMigrationProgress>? progress, CancellationToken cancellationToken)
    {
        var diagnostics = shapefiles.GetDiagnosticsForLayer(layer);
        var features = shapefiles.ReadFeatures(layer, maximumRecords: null, includeSystemFields: true, includeEmpty: true);
        var inserted = 0;
        var empty = 0;
        var invalid = 0;
        var started = DateTime.UtcNow;
        var processed = 0;
        progress?.Report(new ShapefileMigrationProgress(layer, 0, features.Count, 0, 0, 0, TimeSpan.Zero));

        foreach (var feature in features)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            if (feature.Geometry is null || feature.Geometry.IsEmpty)
            {
                empty++;
                ReportProgress();
                continue;
            }
            if (!feature.Geometry.IsValid)
            {
                invalid++;
                ReportProgress();
                continue;
            }

            feature.Geometry.SRID = 4326;
            switch (layer)
            {
                case "manzanas": dbContext.Manzanas.Add(new Manzana { IdOrigen = Int(feature, "Id"), UvMza = Text(feature, "UV_MZA"), Uv = Text(feature, "UV"), Mza = Text(feature, "MZA"), Geom = feature.Geometry }); break;
                case "lotes": dbContext.Lotes.Add(new Lote { IdOrigen = Int(feature, "Id"), NroLote = Text(feature, "NroLote"), Geom = feature.Geometry }); break;
                case "codigos-fijos": dbContext.CodigosFijos.Add(new CodigoFijo { CodFSql = Int(feature, "CodF_SQL"), CodFSig = Text(feature, "CodF_SIG"), CodFijoValor = Int(feature, "CodFijo"), Nombre = Text(feature, "Nombre"), Longitud = Double(feature, "Longi"), Latitud = Double(feature, "Latid"), Estado = 1, FechaCambioEstado = DateTime.UtcNow, Geom = feature.Geometry }); break;
                case "vias": dbContext.Vias.Add(new Via { ObjectId = Int(feature, "OBJECTID"), Nombre = Text(feature, "Nombre"), TipoVia = Text(feature, "type"), OsmId = Text(feature, "OSMID"), Geom = feature.Geometry }); break;
            }
            inserted++;
            if (inserted % 500 == 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            }
            ReportProgress();
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }

        var databaseRecords = await CountRecordsAsync(layer, cancellationToken);
        progress?.Report(new ShapefileMigrationProgress(layer, processed, features.Count, inserted, empty + invalid, 0, DateTime.UtcNow - started));
        return new ShapefileLayerMigrationResult(layer, diagnostics.RecordCount, inserted, empty, invalid, databaseRecords);

        void ReportProgress()
        {
            if (processed % 25 == 0 || processed == features.Count) progress?.Report(new ShapefileMigrationProgress(layer, processed, features.Count, inserted, empty + invalid, 0, DateTime.UtcNow - started));
        }
    }

    private Task<int> CountRecordsAsync(string layer, CancellationToken cancellationToken) => layer switch
    {
        "manzanas" => dbContext.Manzanas.CountAsync(cancellationToken),
        "lotes" => dbContext.Lotes.CountAsync(cancellationToken),
        "codigos-fijos" => dbContext.CodigosFijos.CountAsync(cancellationToken),
        "vias" => dbContext.Vias.CountAsync(cancellationToken),
        _ => Task.FromResult(0)
    };

    private static object? Value(ShapefileFeature feature, string name) => feature.Attributes.FirstOrDefault(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
    private static string? Text(ShapefileFeature feature, string name) => Value(feature, name)?.ToString()?.Trim();
    private static int? Int(ShapefileFeature feature, string name) => Value(feature, name) is { } value && int.TryParse(value.ToString(), out var result) ? result : null;
    private static double? Double(ShapefileFeature feature, string name) => Value(feature, name) is { } value && double.TryParse(value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
}
