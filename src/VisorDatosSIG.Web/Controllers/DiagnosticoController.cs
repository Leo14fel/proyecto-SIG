using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using VisorDatosSIG.Infrastructure.Shapefiles;

namespace VisorDatosSIG.Web.Controllers;

[ApiController]
[Route("api/diagnostico/capas")]
public sealed class DiagnosticoController : ControllerBase
{
    private readonly IShapefileDiagnosticService _diagnostics;
    private readonly IWebHostEnvironment _environment;

    public DiagnosticoController(IShapefileDiagnosticService diagnostics, IWebHostEnvironment environment)
    {
        _diagnostics = diagnostics;
        _environment = environment;
    }

    [HttpGet]
    public IActionResult GetLayers()
    {
        if (!_environment.IsDevelopment()) return NotFound();

        return Ok(_diagnostics.GetOfficialLayers().Select(layer =>
        {
            var diagnostics = _diagnostics.GetDiagnosticsForLayer(layer.Key);
            return new
            {
                layer.Key,
                layer.Name,
                diagnostics.RecordCount,
                diagnostics.GeometryType,
                BoundingBox = diagnostics.BoundingBox is null ? null : new[] { diagnostics.BoundingBox.MinX, diagnostics.BoundingBox.MinY, diagnostics.BoundingBox.MaxX, diagnostics.BoundingBox.MaxY },
                diagnostics.IsPrjValid
            };
        }));
    }

    [HttpGet("{layerKey}/geojson")]
    public IActionResult GetGeoJson(string layerKey, [FromQuery] double? minX, [FromQuery] double? minY, [FromQuery] double? maxX, [FromQuery] double? maxY)
    {
        if (!_environment.IsDevelopment()) return NotFound();

        try
        {
            var hasBounds = minX.HasValue && minY.HasValue && maxX.HasValue && maxY.HasValue;
            if (layerKey.Equals("lotes", StringComparison.OrdinalIgnoreCase) && !hasBounds)
            {
                return BadRequest(new { message = "La capa Lotes requiere una extensión (bbox) para proteger el rendimiento del visor." });
            }

            var bounds = hasBounds ? new Envelope(minX!.Value, maxX!.Value, minY!.Value, maxY!.Value) : null;
            var isLots = layerKey.Equals("lotes", StringComparison.OrdinalIgnoreCase);
            var features = _diagnostics.ReadFeatures(layerKey, bounds, isLots ? 0.00001 : null, isLots ? 3500 : null);
            return Ok(new
            {
                type = "FeatureCollection",
                features = features.Select(feature => new { type = "Feature", properties = feature.Attributes, geometry = ToGeoJsonGeometry(feature.Geometry) }),
                metadata = new { source = "SHP", isLots, maximumRecords = isLots ? 3500 : (int?)null }
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "La capa diagnóstica solicitada no existe." });
        }
        catch (Exception exception)
        {
            return Problem(title: "No fue posible leer la capa SHP.", detail: exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static object ToGeoJsonGeometry(Geometry geometry) => geometry switch
    {
        Point point => new { type = "Point", coordinates = Coordinate(point.Coordinate) },
        MultiPoint points => new { type = "MultiPoint", coordinates = points.Geometries.Cast<Point>().Select(point => Coordinate(point.Coordinate)) },
        LineString line => new { type = "LineString", coordinates = line.Coordinates.Select(Coordinate) },
        MultiLineString lines => new { type = "MultiLineString", coordinates = lines.Geometries.Cast<LineString>().Select(line => line.Coordinates.Select(Coordinate)) },
        Polygon polygon => new { type = "Polygon", coordinates = PolygonCoordinates(polygon) },
        MultiPolygon polygons => new { type = "MultiPolygon", coordinates = polygons.Geometries.Cast<Polygon>().Select(PolygonCoordinates) },
        _ => throw new NotSupportedException($"El tipo geométrico '{geometry.GeometryType}' no está soportado por el visor.")
    };

    private static IEnumerable<IEnumerable<double[]>> PolygonCoordinates(Polygon polygon) =>
        new[] { polygon.ExteriorRing }.Concat(polygon.InteriorRings).Select(ring => ring.Coordinates.Select(Coordinate));

    private static double[] Coordinate(Coordinate coordinate) => [coordinate.X, coordinate.Y];
}
