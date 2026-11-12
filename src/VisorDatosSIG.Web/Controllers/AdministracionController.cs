using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Domain.Entities;
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
    public async Task<IActionResult> AgregarRolOperativo(int id, string role, CancellationToken cancellationToken)
    {
        if (role != WaterOperations.Cutter && role != WaterOperations.Reconnector) return BadRequest();
        if (!await dbContext.Usuarios.AnyAsync(u => u.IdUsuario == id && u.Activo, cancellationToken)) return NotFound();
        var roleId = await dbContext.Roles.Where(r => r.NombreRol == role && r.Estado)
            .Select(r => (int?)r.IdRol).SingleOrDefaultAsync(cancellationToken);
        if (!roleId.HasValue)
        {
            TempData["RoleMessage"] = "El rol no está activo. Revisa la instalación de operaciones.";
            return RedirectToAction(nameof(Usuarios));
        }
        if (!await dbContext.UsuariosRoles.AnyAsync(r => r.IdUsuario == id && r.IdRol == roleId.Value, cancellationToken))
        {
            dbContext.UsuariosRoles.Add(new UsuarioRol { IdUsuario = id, IdRol = roleId.Value });
            try { await dbContext.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { }
        }
        TempData["RoleMessage"] = "Rol operativo asignado. El trabajador debe iniciar sesión nuevamente.";
        return RedirectToAction(nameof(Usuarios));
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
