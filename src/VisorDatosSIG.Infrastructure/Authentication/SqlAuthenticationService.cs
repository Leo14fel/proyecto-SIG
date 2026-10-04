using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using VisorDatosSIG.Application.Authentication;
using VisorDatosSIG.Infrastructure.Persistence;

namespace VisorDatosSIG.Infrastructure.Authentication;

public sealed class SqlAuthenticationService(VisorDatosSigDbContext dbContext) : IAuthenticationService
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        var normalizedLogin = login.Trim();
        if (string.IsNullOrWhiteSpace(normalizedLogin) || string.IsNullOrEmpty(password)) return null;

        var user = await dbContext.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Login == normalizedLogin, cancellationToken);
        if (user is null || !user.Activo || !VerifyPassword(password, user.PasswordSalt, user.PasswordHash, user.Iteraciones)) return null;

        var roles = await (from userRole in dbContext.UsuariosRoles.AsNoTracking()
                           join role in dbContext.Roles.AsNoTracking() on userRole.IdRol equals role.IdRol
                           where userRole.IdUsuario == user.IdUsuario && role.Estado
                           select role.NombreRol).ToListAsync(cancellationToken);
        return new AuthenticatedUser(user.IdUsuario, user.Login, user.Nombre, roles);
    }

    public static bool VerifyPassword(string password, byte[] salt, byte[] expectedHash, int iterations)
    {
        if (salt.Length != 32 || expectedHash.Length != 32 || iterations <= 0) return false;
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
