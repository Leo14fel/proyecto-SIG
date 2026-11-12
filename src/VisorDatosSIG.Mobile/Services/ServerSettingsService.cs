using VisorDatosSIG.Mobile.Models;

namespace VisorDatosSIG.Mobile.Services;

public sealed class ServerSettingsService
{
    private const string ServerUrlKey = "server-url";
#if DEBUG
    private const string DebugDefaultServerUrl = "http://192.168.0.17:8080";
#endif

    public Uri? GetServerUri()
    {
        var savedUrl = Preferences.Default.Get<string?>(ServerUrlKey, null);
        if (TryNormalize(savedUrl, out var serverUri, out _)) return serverUri;

#if DEBUG
        if (TryNormalize(DebugDefaultServerUrl, out serverUri, out _))
        {
            Save(serverUri!);
            return serverUri;
        }
#endif

        return null;
    }

    public ServerConfiguration? GetConfiguration()
    {
        var serverUri = GetServerUri();
        return serverUri is null ? null : new ServerConfiguration(serverUri.AbsoluteUri);
    }

    public void Save(Uri serverUri) => Preferences.Default.Set(ServerUrlKey, serverUri.AbsoluteUri);

    public static bool TryNormalize(string? value, out Uri? serverUri, out string? error)
    {
        serverUri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Ingresa la URL del servidor.";
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(parsed.Host))
        {
            error = "Usa una URL HTTP o HTTPS válida.";
            return false;
        }

        if (parsed.IsLoopback)
        {
            error = "Usa 10.0.2.2 en el emulador o la IP de tu equipo en la red Wi-Fi; localhost no apunta al servidor desde Android.";
            return false;
        }

        var normalizedPath = parsed.AbsolutePath.EndsWith('/') ? parsed.AbsolutePath : $"{parsed.AbsolutePath}/";
        var builder = new UriBuilder(parsed) { Path = normalizedPath, Query = string.Empty, Fragment = string.Empty };
        serverUri = builder.Uri;
        return true;
    }
}
