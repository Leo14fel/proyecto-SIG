using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisorDatosSIG.Infrastructure.Authentication;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

[Authorize]
public sealed class CuentaPerfilController(VisorDatosSigDbContext dbContext) : Controller
{
    [HttpGet("Cuenta/MiPerfil")]
    public async Task<IActionResult> MiPerfil(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return Challenge();
        SetProfileDetails(user);
        return View(new ChangePasswordViewModel());
    }

    [HttpPost("Cuenta/MiPerfil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MiPerfil(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null) return Challenge();
        if (!SqlAuthenticationService.VerifyPassword(model.CurrentPassword, user.PasswordSalt, user.PasswordHash, user.Iteraciones)) ModelState.AddModelError(nameof(model.CurrentPassword), "La contraseña actual no es correcta.");
        if (!ModelState.IsValid)
        {
            SetProfileDetails(user);
            return View(model);
        }
        var password = SqlAuthenticationService.HashPassword(model.NewPassword);
        user.PasswordSalt = password.Salt;
        user.PasswordHash = password.Hash;
        user.Iteraciones = password.Iterations;
        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Contraseña actualizada.";
        return RedirectToAction(nameof(MiPerfil));
    }

    private Task<VisorDatosSIG.Domain.Entities.Usuario?> CurrentUserAsync(CancellationToken cancellationToken)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(rawId, out var id) ? dbContext.Usuarios.SingleOrDefaultAsync(user => user.IdUsuario == id, cancellationToken) : Task.FromResult<VisorDatosSIG.Domain.Entities.Usuario?>(null);
    }

    private void SetProfileDetails(VisorDatosSIG.Domain.Entities.Usuario user)
    {
        ViewBag.Login = user.Login;
        ViewBag.Nombre = user.Nombre;
        ViewBag.Estado = user.Activo ? "Activo" : "Inactivo";
        ViewBag.Roles = string.Join(", ", User.FindAll(ClaimTypes.Role).Select(claim => claim.Value));
    }
}
