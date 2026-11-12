using Microsoft.Extensions.DependencyInjection;
using VisorDatosSIG.Mobile.Services;

namespace VisorDatosSIG.Mobile.Views;

public partial class ViewerPage : ContentPage
{
    private readonly ServerSettingsService _settings;
    private readonly ServerConnectivityService _connectivity;
    private readonly IServiceProvider _services;
    private bool _openingExternalLink;
    private bool _hasCheckedInitialConnection;

    public ViewerPage(ServerSettingsService settings, ServerConnectivityService connectivity, IServiceProvider services)
    {
        InitializeComponent();
        _settings = settings;
        _connectivity = connectivity;
        _services = services;
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

        var serverUri = _settings.GetServerUri();
        if (serverUri is not null && Uri.Compare(serverUri, targetUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
        {
            return;
        }

        e.Cancel = true;
        _openingExternalLink = true;
        try
        {
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
        if (e.Result is WebNavigationResult.Failure or WebNavigationResult.Timeout)
        {
            ShowServerUnavailable();
        }
    }

    private void ShowServerUnavailable() => ShowConnectionError("Servidor no disponible.");

    private void ShowConnectionError(string message)
    {
        ConnectionErrorLabel.Text = message;
        ConnectionErrorPanel.IsVisible = true;
    }
}
