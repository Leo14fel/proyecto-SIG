using NetTopologySuite.Geometries;

namespace VisorDatosSIG.Web;

public static class GeoJsonEncoder
{
    public static object Encode(Geometry geometry) => geometry switch
    {
        Point point => new { type = "Point", coordinates = Coordinate(point.Coordinate) },
        MultiPoint points => new { type = "MultiPoint", coordinates = points.Geometries.Cast<Point>().Select(point => Coordinate(point.Coordinate)) },
        LineString line => new { type = "LineString", coordinates = line.Coordinates.Select(Coordinate) },
        MultiLineString lines => new { type = "MultiLineString", coordinates = lines.Geometries.Cast<LineString>().Select(line => line.Coordinates.Select(Coordinate)) },
        Polygon polygon => new { type = "Polygon", coordinates = PolygonCoordinates(polygon) },
        MultiPolygon polygons => new { type = "MultiPolygon", coordinates = polygons.Geometries.Cast<Polygon>().Select(PolygonCoordinates) },
        _ => throw new NotSupportedException($"El tipo geométrico '{geometry.GeometryType}' no está soportado por GeoJSON.")
    };

    private static IEnumerable<IEnumerable<double[]>> PolygonCoordinates(Polygon polygon) =>
        new[] { polygon.ExteriorRing }.Concat(polygon.InteriorRings).Select(ring => ring.Coordinates.Select(Coordinate));

    private static double[] Coordinate(Coordinate coordinate) => [coordinate.X, coordinate.Y];
}
