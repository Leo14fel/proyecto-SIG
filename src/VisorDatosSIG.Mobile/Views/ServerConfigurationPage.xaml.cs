using Microsoft.Extensions.DependencyInjection;
using VisorDatosSIG.Mobile.Services;

namespace VisorDatosSIG.Mobile.Views;

public partial class ServerConfigurationPage : ContentPage
{
    private readonly ServerSettingsService _settings;
    private readonly ServerConnectivityService _connectivity;
    private readonly IServiceProvider _services;

    public ServerConfigurationPage(ServerSettingsService settings, ServerConnectivityService connectivity, IServiceProvider services)
    {
        InitializeComponent();
        _settings = settings;
        _connectivity = connectivity;
        _services = services;
        ServerUrlEntry.Text = _settings.GetConfiguration()?.Url;
    }

    private async void OnTestConnectionClicked(object? sender, EventArgs e) => await CheckConnectionAsync(saveOnSuccess: false);

    private async void OnSaveClicked(object? sender, EventArgs e) => await CheckConnectionAsync(saveOnSuccess: true);

    private async Task CheckConnectionAsync(bool saveOnSuccess)
    {
        if (!ServerSettingsService.TryNormalize(ServerUrlEntry.Text, out var serverUri, out var validationError))
        {
            ShowStatus(validationError!, isError: true);
            return;
        }

        SetBusy(true);
        var result = await _connectivity.CheckAsync(serverUri!);
        SetBusy(false);
        ShowStatus(result.Message, !result.IsAvailable);

        if (result.IsAvailable && saveOnSuccess)
        {
            _settings.Save(serverUri!);
            await Navigation.PushAsync(_services.GetRequiredService<ViewerPage>());
        }
    }

    private void SetBusy(bool isBusy)
    {
        BusyIndicator.IsVisible = isBusy;
        BusyIndicator.IsRunning = isBusy;
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = isError ? Color.FromArgb("B91C1C") : Color.FromArgb("0F766E");
        StatusLabel.IsVisible = true;
    }
}
