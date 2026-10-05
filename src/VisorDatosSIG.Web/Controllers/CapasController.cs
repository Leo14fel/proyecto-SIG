using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using VisorDatosSIG.Infrastructure.Persistence;

namespace VisorDatosSIG.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/capas")]
public sealed class CapasController(ISqlLayerService layers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLayers(CancellationToken cancellationToken) => Ok(await layers.GetLayersAsync(cancellationToken));

    [HttpGet("{capa}/geojson")]
    public async Task<IActionResult> GetGeoJson(string capa, [FromQuery] double? minX, [FromQuery] double? minY, [FromQuery] double? maxX, [FromQuery] double? maxY, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var suppliedBounds = new[] { minX, minY, maxX, maxY };
        if (suppliedBounds.Any(value => value.HasValue) && suppliedBounds.Any(value => !value.HasValue)) return BadRequest(new { message = "bbox requiere minX, minY, maxX y maxY." });
        if (suppliedBounds.All(value => value.HasValue) && (!double.IsFinite(minX!.Value) || !double.IsFinite(minY!.Value) || !double.IsFinite(maxX!.Value) || !double.IsFinite(maxY!.Value) || minX > maxX || minY > maxY)) return BadRequest(new { message = "bbox no es válido." });
        var bbox = minX.HasValue ? new Envelope(minX.Value, maxX!.Value, minY!.Value, maxY!.Value) : null;
        if (capa.Equals("lotes", StringComparison.OrdinalIgnoreCase) && bbox is null) return BadRequest(new { message = "La capa Lotes requiere bbox." });

        try
        {
            var maximumRecords = capa.Equals("lotes", StringComparison.OrdinalIgnoreCase) ? 3500 : 10000;
            var features = await layers.GetFeaturesAsync(capa, bbox, limit ?? maximumRecords, cancellationToken);
            return Ok(new { type = "FeatureCollection", features = features.Select(feature => new { type = "Feature", id = feature.Id, properties = feature.Properties, geometry = GeoJsonEncoder.Encode(feature.Geometry) }), metadata = new { returned = features.Count, maximumRecords } });
        }
        catch (KeyNotFoundException) { return NotFound(new { message = "La capa solicitada no existe." }); }
    }

    [HttpGet("{capa}/{id:int}")]
    public async Task<IActionResult> GetFeature(string capa, int id, CancellationToken cancellationToken)
    {
        try
        {
            var feature = await layers.GetFeatureAsync(capa, id, cancellationToken);
            return feature is null ? NotFound() : Ok(new { type = "Feature", id = feature.Id, properties = feature.Properties, geometry = GeoJsonEncoder.Encode(feature.Geometry) });
        }
        catch (KeyNotFoundException) { return NotFound(new { message = "La capa solicitada no existe." }); }
    }
}
