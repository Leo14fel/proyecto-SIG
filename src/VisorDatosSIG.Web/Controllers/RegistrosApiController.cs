using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Infrastructure.Persistence;

namespace VisorDatosSIG.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/registros")]
public sealed class RegistrosApiController(ISqlLayerService layers) : ControllerBase
{
    private static readonly HashSet<string> LayerKeys = ["manzanas", "lotes", "codigos-fijos", "vias"];

    [HttpGet]
    public async Task<IActionResult> Browse([FromQuery] string? capa, [FromQuery] string? q, [FromQuery] string? campo, [FromQuery] string? operador, [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 25, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(capa) || (!LayerKeys.Contains(capa) && !capa.Equals("todas", StringComparison.OrdinalIgnoreCase))) return BadRequest(new { message = "La capa solicitada no existe." });
        if ((q?.Trim().Length ?? 0) > 100) return BadRequest(new { message = "q no puede superar 100 caracteres." });
        if (pagina < 1) return BadRequest(new { message = "pagina debe ser mayor que cero." });
        if (!string.IsNullOrWhiteSpace(campo) && string.IsNullOrWhiteSpace(q)) return BadRequest(new { message = "Los filtros por campo requieren un valor de búsqueda." });
        if (capa.Equals("todas", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(campo)) return BadRequest(new { message = "Los filtros por campo requieren una capa específica." });

        try
        {
            var pageSize = Math.Clamp(tamanoPagina, 1, 100);
            if (capa.Equals("todas", StringComparison.OrdinalIgnoreCase))
            {
                var perLayer = Math.Max(1, pageSize / LayerKeys.Count);
                var pages = new List<SqlSearchPage>();
                foreach (var key in LayerKeys) pages.Add(await layers.BrowseAsync(key, q, pagina, perLayer, cancellationToken: cancellationToken));
                return Ok(new { capa, q = q?.Trim() ?? string.Empty, campo, operador, pagina, tamanoPagina = pageSize, total = pages.Sum(result => result.Total), items = pages.SelectMany(result => result.Items) });
            }
            var result = await layers.BrowseAsync(capa, q, pagina, pageSize, campo, operador, cancellationToken);
            return Ok(new { capa, q = q?.Trim() ?? string.Empty, campo, operador, pagina, tamanoPagina = pageSize, total = result.Total, items = result.Items });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("{capa}/{id:int}")]
    public async Task<IActionResult> Get(string capa, int id, CancellationToken cancellationToken)
    {
        if (!LayerKeys.Contains(capa)) return NotFound(new { message = "La capa solicitada no existe." });
        var feature = await layers.GetFeatureAsync(capa, id, cancellationToken);
        return feature is null ? NotFound(new { message = "El registro no existe." }) : Ok(new { capa, id = feature.Id, properties = feature.Properties });
    }
}
