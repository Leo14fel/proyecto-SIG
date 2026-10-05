using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Infrastructure.Persistence;

namespace VisorDatosSIG.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/busqueda")]
public sealed class BusquedaController(ISqlLayerService layers) : ControllerBase
{
    private static readonly string[] AllLayers = ["manzanas", "lotes", "codigos-fijos", "vias"];
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? capa, [FromQuery] string? q, [FromQuery] string? campo, [FromQuery] string? operador, [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 25, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(capa) || string.IsNullOrWhiteSpace(q)) return BadRequest(new { message = "capa y q son obligatorios." });
        if (q.Trim().Length > 100) return BadRequest(new { message = "q no puede superar 100 caracteres." });
        if (pagina < 1) return BadRequest(new { message = "pagina debe ser mayor que cero." });

        try
        {
            if (capa.Equals("todas", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(campo)) return BadRequest(new { message = "Los filtros por campo requieren una capa específica." });
            var result = await layers.SearchAsync(capa, q, pagina, tamanoPagina, campo, operador, cancellationToken);
            return Ok(new { capa, q, campo, operador, pagina, tamanoPagina = Math.Clamp(tamanoPagina, 1, 100), total = result.Total, items = result.Items });
        }
        catch (KeyNotFoundException) { return NotFound(new { message = "La capa solicitada no existe." }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("exportar")]
    public async Task<IActionResult> Export([FromQuery] string? capa, [FromQuery] string? q, [FromQuery] string? campo, [FromQuery] string? operador, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(capa) || string.IsNullOrWhiteSpace(q)) return BadRequest(new { message = "capa y q son obligatorios." });
        if (q.Trim().Length > 100) return BadRequest(new { message = "q no puede superar 100 caracteres." });
        if (capa.Equals("todas", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(campo)) return BadRequest(new { message = "Los filtros por campo requieren una capa específica." });

        try
        {
            const int maximumRows = 10_000;
            var lines = new List<string> { "Capa,Identificador,Campo,Valor" };
            foreach (var layer in capa.Equals("todas", StringComparison.OrdinalIgnoreCase) ? AllLayers : [capa])
            {
                for (var page = 1; lines.Count <= maximumRows; page++)
                {
                    var result = await layers.SearchAsync(layer, q, page, 100, campo, operador, cancellationToken);
                    foreach (var item in result.Items)
                    {
                        foreach (var property in item.Properties.Where(property => property.Key is not "Geom" and not "Hash"))
                        {
                            lines.Add(string.Join(',', Csv(layer), Csv(item.Id), Csv(property.Key), Csv(property.Value)));
                            if (lines.Count > maximumRows) break;
                        }
                    }
                    if (result.Items.Count < 100 || lines.Count > maximumRows) break;
                }
            }

            var content = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines))).ToArray();
            var name = $"VisorDatosSIG_Resultados_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
            return File(content, "text/csv; charset=utf-8", name);
        }
        catch (KeyNotFoundException) { return NotFound(new { message = "La capa solicitada no existe." }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private static string Csv(object? value)
    {
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (text.StartsWith('=') || text.StartsWith('+') || text.StartsWith('-') || text.StartsWith('@')) text = "'" + text;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
