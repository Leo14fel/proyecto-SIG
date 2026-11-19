using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.Authentication;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

public sealed class CuentaController(
    VisorDatosSIG.Application.Authentication.IAuthenticationService authenticationService,
    ILogger<CuentaController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = LoginDestination(returnUrl) });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        AuthenticatedUser? user;
        try
        {
            user = await authenticationService.AuthenticateAsync(model.Login, model.Password, cancellationToken);
        }
        catch (SqlException error) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(error, "SQL Server falló al validar el inicio de sesión.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ModelState.AddModelError(string.Empty, "Por el momento el sistema no puede consultar la base de datos. Intenta de nuevo en unos minutos.");
            return View(model);
        }
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Nombre),
            new("login", user.Login)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(LoginDestination(model.ReturnUrl) ?? Url.Action("Index", "Home")!);
    }

    private string? LoginDestination(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) &&
        !string.Equals(returnUrl, Url.Action("Error", "Home"), StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : null;

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccesoDenegado() => View();
}
