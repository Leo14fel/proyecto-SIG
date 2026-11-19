using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VisorDatosSIG.Domain.Entities;
using VisorDatosSIG.Infrastructure.Authentication;
using VisorDatosSIG.Infrastructure.Persistence;

namespace VisorDatosSIG.Web;

internal sealed class DemoAccountSeeder(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<DemoAccountSeeder> logger) : BackgroundService
{
    private static readonly (string Login, string Role)[] Accounts =
    [
        ("demo.admin", "Administrador"),
        ("demo.catastro", "Catastro"),
        ("demo.lectura", "Lecturador"),
        ("demo.corte", "Cortador"),
        ("demo.reconexion", "Reconexion"),
        ("demo.consulta", "Consultor")
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var (login, roleName) in Accounts)
        {
            var password = configuration[$"DemoAccounts:{roleName}"];
            if (string.IsNullOrEmpty(password))
                continue;

            if (password.Length < 12)
            {
                logger.LogWarning("La contraseña de {Login} debe tener al menos 12 caracteres.", login);
                continue;
            }

            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<VisorDatosSigDbContext>();
                var strategy = new SqlServerRetryingExecutionStrategy(db, maxRetryCount: 2);

                await strategy.ExecuteAsync(async cancellationToken =>
                {
                    db.ChangeTracker.Clear();
                    await using var transaction = await db.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable, cancellationToken);

                    if (await db.Usuarios.AnyAsync(user => user.Login == login, cancellationToken))
                    {
                        logger.LogInformation("La cuenta {Login} ya existe. Se conservó su configuración.", login);
                        return;
                    }

                    var role = await db.Roles.SingleOrDefaultAsync(
                        item => item.NombreRol == roleName, cancellationToken);
                    if (role is { Estado: false })
                    {
                        logger.LogWarning("La cuenta {Login} requiere el rol activo {Role}.", login, roleName);
                        return;
                    }

                    if (role is null)
                    {
                        role = new Rol { NombreRol = roleName, Estado = true };
                        db.Roles.Add(role);
                    }

                    var (salt, hash, iterations) = SqlAuthenticationService.HashPassword(password);
                    var user = new Usuario
                    {
                        Login = login,
                        Nombre = $"Demo {roleName}",
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        Iteraciones = iterations,
                        FechaRegistro = DateTime.UtcNow,
                        Activo = true
                    };
                    db.Usuarios.Add(user);
                    await db.SaveChangesAsync(cancellationToken);
                    db.UsuariosRoles.Add(new UsuarioRol { IdUsuario = user.IdUsuario, IdRol = role.IdRol });
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    logger.LogInformation("Se creó la cuenta {Login} con el rol {Role}.", login, roleName);
                }, stoppingToken);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested &&
                error is SqlException or DbUpdateException or RetryLimitExceededException)
            {
                logger.LogError(error, "No se pudo crear {Login} en SQL Server.", login);
            }
        }
    }
}
