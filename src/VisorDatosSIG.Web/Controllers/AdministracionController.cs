using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class AdministracionController(VisorDatosSigDbContext dbContext) : Controller
{
    public async Task<IActionResult> Usuarios(CancellationToken cancellationToken)
    {
        var users = await dbContext.Usuarios.AsNoTracking().OrderBy(user => user.Login).ToListAsync(cancellationToken);
        var roleRows = await (from assignment in dbContext.UsuariosRoles.AsNoTracking()
                              join role in dbContext.Roles.AsNoTracking() on assignment.IdRol equals role.IdRol
                              select new { assignment.IdUsuario, role.NombreRol }).ToListAsync(cancellationToken);
        return View(users.Select(user => new UserAdministrationItem(user.IdUsuario, user.Login, user.Nombre, string.Join(", ", roleRows.Where(row => row.IdUsuario == user.IdUsuario).Select(row => row.NombreRol)), user.Activo)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Usuarios.SingleOrDefaultAsync(item => item.IdUsuario == id, cancellationToken);
        if (user is null) return NotFound();
        user.Activo = !user.Activo;
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Usuarios));
    }
}
