using VisorDatosSIG.Mobile.Services;
using VisorDatosSIG.Mobile.Views;

namespace VisorDatosSIG.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<ServerSettingsService>();
        builder.Services.AddSingleton<ServerConnectivityService>();
        builder.Services.AddTransient<ServerConfigurationPage>();
        builder.Services.AddTransient<ViewerPage>();
        return builder.Build();
    }
}
