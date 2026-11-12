using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VisorDatosSIG.Domain.Entities;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

[Authorize]
public class HomeController(ISqlLayerService layers, VisorDatosSigDbContext db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Administrador") && (User.IsInRole(WaterOperations.Cutter) || User.IsInRole(WaterOperations.Reconnector)))
            return RedirectToAction("Campo", "Operaciones");
        ViewBag.ConnectionCounts = await db.CodigosFijos.GroupBy(x => x.Estado)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.State, x => x.Count, cancellationToken);
        var layerCards = await layers.GetLayersAsync(cancellationToken);
        return View(layerCards.Select(layer => new LayerCardViewModel(layer.Name, layer.Count)));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Ayuda()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
