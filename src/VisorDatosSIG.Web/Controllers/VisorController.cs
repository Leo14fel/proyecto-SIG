using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace VisorDatosSIG.Web.Controllers;

[Authorize]
public sealed class VisorController : Controller
{
    public IActionResult Index() => View();
}
