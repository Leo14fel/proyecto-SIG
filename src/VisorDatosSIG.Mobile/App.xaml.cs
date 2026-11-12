using VisorDatosSIG.Mobile.Services;
using VisorDatosSIG.Mobile.Views;

namespace VisorDatosSIG.Mobile;

public partial class App : Application
{
    private readonly ServerSettingsService _serverSettings;
    private readonly IServiceProvider _services;

    public App(ServerSettingsService serverSettings, IServiceProvider services)
    {
        InitializeComponent();
        _serverSettings = serverSettings;
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Page rootPage = _serverSettings.GetServerUri() is null
            ? _services.GetRequiredService<ServerConfigurationPage>()
            : _services.GetRequiredService<ViewerPage>();
        return new Window(new NavigationPage(rootPage));
    }
}
