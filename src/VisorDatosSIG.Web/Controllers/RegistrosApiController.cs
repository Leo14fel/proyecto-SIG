using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Infrastructure.Persistence;

namespace VisorDatosSIG.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/registros")]
public sealed class RegistrosApiController(ISqlLayerService layers) : ControllerBase
{
    private static readonly string[] LayerKeys = ["manzanas", "lotes", "codigos-fijos", "vias"];

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
            if ((long)(pagina - 1) * pageSize > int.MaxValue) return BadRequest(new { message = "La página solicitada es demasiado grande." });
            if (capa.Equals("todas", StringComparison.OrdinalIgnoreCase))
            {
                var offset = (long)(pagina - 1) * pageSize;
                var total = 0;
                var items = new List<SqlSearchResult>(pageSize);
                foreach (var key in LayerKeys)
                {
                    var first = await layers.BrowseAsync(key, q, 1, pageSize, cancellationToken: cancellationToken);
                    total += first.Total;
                    if (offset >= first.Total) { offset -= first.Total; continue; }
                    if (items.Count == pageSize) continue;

                    var localPage = (int)(offset / pageSize) + 1;
                    var skip = (int)(offset % pageSize);
                    var current = localPage == 1 ? first : await layers.BrowseAsync(key, q, localPage, pageSize, cancellationToken: cancellationToken);
                    items.AddRange(current.Items.Skip(skip).Take(pageSize - items.Count));
                    if (skip > 0 && items.Count < pageSize && (long)localPage * pageSize < first.Total)
                    {
                        var following = await layers.BrowseAsync(key, q, localPage + 1, pageSize, cancellationToken: cancellationToken);
                        items.AddRange(following.Items.Take(pageSize - items.Count));
                    }
                    offset = 0;
                }
                return Ok(new { capa, q = q?.Trim() ?? string.Empty, campo, operador, pagina, tamanoPagina = pageSize, total, items });
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
