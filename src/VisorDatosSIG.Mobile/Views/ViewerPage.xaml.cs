using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using VisorDatosSIG.Mobile.Services;

namespace VisorDatosSIG.Mobile.Views;

public partial class ViewerPage : ContentPage
{
    private readonly ServerSettingsService _settings;
    private readonly ServerConnectivityService _connectivity;
    private readonly IServiceProvider _services;
    private bool _openingExternalLink;
    private bool _hasCheckedInitialConnection;
    private bool _fieldPage;
    private bool _fieldRequest;
    private int _navigationVersion;

    public ViewerPage(ServerSettingsService settings, ServerConnectivityService connectivity, IServiceProvider services)
    {
        InitializeComponent();
        _settings = settings;
        _connectivity = connectivity;
        _services = services;
        Browser.UserAgent = "Mozilla/5.0 (Linux; Android) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36 VisorDatosSIGMobile";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_hasCheckedInitialConnection) return;

        _hasCheckedInitialConnection = true;
        _ = OpenServerAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        if (Browser.CanGoBack)
        {
            Browser.GoBack();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async Task OpenServerAsync()
    {
        var serverUri = _settings.GetServerUri();
        if (serverUri is null)
        {
            await Navigation.PushAsync(_services.GetRequiredService<ServerConfigurationPage>());
            return;
        }

        var connection = await _connectivity.CheckAsync(serverUri);
        if (!connection.IsAvailable)
        {
            ShowServerUnavailable();
            return;
        }

        ConnectionErrorPanel.IsVisible = false;
        Browser.Source = new UrlWebViewSource { Url = serverUri.AbsoluteUri };
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await OpenServerAsync();
    }

    private async void OnChangeServerClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(_services.GetRequiredService<ServerConfigurationPage>());
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (_openingExternalLink || !Uri.TryCreate(e.Url, UriKind.Absolute, out var targetUri)) return;

        if (targetUri.Scheme == "visordatos-field")
        {
            e.Cancel = true;
            if (_fieldPage && !_fieldRequest) await HandleFieldRequestAsync(targetUri);
            return;
        }

        var serverUri = _settings.GetServerUri();
        if (serverUri is not null && Uri.Compare(serverUri, targetUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
        {
            if (_fieldPage) StopTracking();
            _fieldPage = false;
            _navigationVersion++;
            return;
        }

        e.Cancel = true;
        _openingExternalLink = true;
        try
        {
            if (targetUri.Scheme != Uri.UriSchemeHttps && targetUri.Scheme != Uri.UriSchemeHttp) return;
            if (_fieldPage && targetUri.Host == "www.google.com" && targetUri.AbsolutePath == "/maps/dir/")
                await Launcher.Default.OpenAsync(targetUri);
            else
                await Microsoft.Maui.ApplicationModel.Browser.Default.OpenAsync(targetUri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception)
        {
            ShowConnectionError("No se pudo abrir el enlace externo.");
        }
        finally
        {
            _openingExternalLink = false;
        }
    }

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        var server = _settings.GetServerUri();
        _fieldPage = e.Result == WebNavigationResult.Success && server is not null
            && Uri.TryCreate(e.Url, UriKind.Absolute, out var target)
            && Uri.Compare(server, target, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0
            && target.AbsolutePath.Equals("/Operaciones/Campo", StringComparison.OrdinalIgnoreCase);
        if (e.Result is WebNavigationResult.Failure or WebNavigationResult.Timeout)
        {
            ShowServerUnavailable();
        }
    }

    private async Task HandleFieldRequestAsync(Uri command)
    {
        _fieldRequest = true;
        var version = _navigationVersion;
        try
        {
            object result;
            try
            {
                result = command.Host switch
                {
                    "gps" => await GetFieldLocationAsync(),
                    "photo" => await TakeFieldPhotoAsync(),
                    "track" => await StartTrackingAsync(command),
                    "stop" => StopTracking(),
                    _ => new { error = "Acción no disponible." }
                };
            }
            catch (Exception)
            {
                result = new { error = "No se pudo usar el GPS o la cámara. Revisa los permisos del teléfono." };
            }
            if (_fieldPage && version == _navigationVersion)
                await Browser.EvaluateJavaScriptAsync($"window.visorNativeResult?.({JsonSerializer.Serialize(result)})");
        }
        finally { _fieldRequest = false; }
    }

    private async Task<object> StartTrackingAsync(Uri command)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return new { error = "El seguimiento durante la navegación requiere Android 8 o superior." };
        var server = _settings.GetServerUri();
        if (server is null || !int.TryParse(command.AbsolutePath.Trim('/'), out var id) || id <= 0)
            return new { error = "Asignación inválida." };
        var token = Uri.UnescapeDataString(command.Fragment.TrimStart('#'));
        if (token.Length is < 1 or > 1024) return new { error = "Vuelve a iniciar sesión." };
        var navigation = _navigationVersion;
        if (await Permissions.RequestAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
            return new { error = "Permite la ubicación para iniciar el recorrido." };
        if (!_fieldPage || navigation != _navigationVersion) return new { error = "La pantalla cambió." };
        var endpoint = new Uri(server, $"/Operaciones/Posicion/{id}");
        var cookie = global::Android.Webkit.CookieManager.Instance?.GetCookie(endpoint.AbsoluteUri);
        if (string.IsNullOrEmpty(cookie)) return new { error = "Vuelve a iniciar sesión." };
        var context = global::Android.App.Application.Context;
        using var intent = new global::Android.Content.Intent(context, typeof(FieldTrackingService));
        intent.PutExtra("endpoint", endpoint.AbsoluteUri);
        intent.PutExtra("cookie", cookie);
        intent.PutExtra("token", token);
        context.StartForegroundService(intent);
        return new { started = true };
    }

    private static object StopTracking()
    {
        var context = global::Android.App.Application.Context;
        using var intent = new global::Android.Content.Intent(context, typeof(FieldTrackingService));
        context.StopService(intent);
        return new { stopped = true };
    }

    private static async Task<object> GetFieldLocationAsync()
    {
        if (await Permissions.RequestAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
            return new { error = "Permite la ubicación precisa para usar el GPS." };
        var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(20)));
        if (location?.Accuracy is null) return new { error = "No se obtuvo una posición con precisión disponible." };
        return new { lat = location.Latitude, lng = location.Longitude, accuracy = location.Accuracy.Value };
    }

    private static async Task<object> TakeFieldPhotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported) return new { error = "No hay una cámara disponible." };
        var file = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
        {
            MaximumWidth = 1600, MaximumHeight = 1600, CompressionQuality = 80
        });
        if (file is null) return new { error = "Captura cancelada." };
        await using var source = await file.OpenReadAsync();
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer);
        if (buffer.Length > 5_000_000) return new { error = "La foto supera 5 MB." };
        return new { photo = Convert.ToBase64String(buffer.ToArray()), name = file.FileName, type = file.ContentType };
    }

    private void ShowServerUnavailable() => ShowConnectionError("Servidor no disponible.");

    private void ShowConnectionError(string message)
    {
        ConnectionErrorLabel.Text = message;
        ConnectionErrorPanel.IsVisible = true;
    }
}
