using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

[Authorize]
public class HomeController(ISqlLayerService layers) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var layerCards = await layers.GetLayersAsync(cancellationToken);
        return View(layerCards.Select(layer => new LayerCardViewModel(layer.Name, layer.Count)));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
