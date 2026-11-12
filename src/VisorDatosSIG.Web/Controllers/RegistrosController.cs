using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

[Authorize]
public sealed class RegistrosController(ISqlLayerService layers) : Controller
{
    private static readonly HashSet<string> LayerKeys = ["manzanas", "lotes", "codigos-fijos", "vias"];

    public IActionResult Index() => View();

    [HttpGet("Registros/{capa}/{id:int}")]
    public async Task<IActionResult> Detalle(string capa, int id, [FromQuery] string? volver, CancellationToken cancellationToken)
    {
        if (!LayerKeys.Contains(capa)) return NotFound();
        var feature = await layers.GetFeatureAsync(capa, id, cancellationToken);
        if (feature is null) return NotFound();
        var name = (await layers.GetLayersAsync(cancellationToken)).First(layer => layer.Key.Equals(capa, StringComparison.OrdinalIgnoreCase)).Name;
        return View(new GeographicRecordDetailViewModel(capa, name, feature.Id, feature.Properties, SafeReturnUrl(volver)));
    }

    private string SafeReturnUrl(string? value) => !string.IsNullOrWhiteSpace(value) && Url.IsLocalUrl(value) ? value : Url.Action(nameof(Index))!;
}
