namespace VisorDatosSIG.Mobile.Services;

public sealed class ServerConnectivityService
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(10);

    public async Task<(bool IsAvailable, string Message)> CheckAsync(Uri serverUri, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = ConnectionTimeout };

        try
        {
            using var response = await client.GetAsync(serverUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return (true, "Servidor disponible.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, "La conexión tardó demasiado. Verifica la URL y la red Wi-Fi.");
        }
        catch (HttpRequestException)
        {
            return (false, "No se pudo conectar con VisorDatosSIG. Verifica que el servidor esté iniciado y que la URL sea correcta.");
        }
    }
}
