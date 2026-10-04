using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VisorDatosSIG.Infrastructure.Shapefiles;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IShapefileDiagnosticService _diagnostics;

    public HomeController(IShapefileDiagnosticService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public IActionResult Index()
    {
        var layers = _diagnostics.GetOfficialLayers()
            .Select(layer => new LayerCardViewModel(layer.Name, _diagnostics.GetDiagnosticsForLayer(layer.Key).RecordCount));
        return View(layers);
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
