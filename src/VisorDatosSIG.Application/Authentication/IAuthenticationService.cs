namespace VisorDatosSIG.Application.Authentication;

public sealed record AuthenticatedUser(int Id, string Login, string Nombre, IReadOnlyList<string> Roles);

public interface IAuthenticationService
{
    Task<AuthenticatedUser?> AuthenticateAsync(string login, string password, CancellationToken cancellationToken = default);
}
