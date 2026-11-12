using System.Text.Json;

namespace VisorDatosSIG.Tests;

public sealed class PwaAssetTests
{
    [Fact]
    public void Manifest_declares_an_installable_standalone_application()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(WebAsset("manifest.webmanifest")));
        var root = manifest.RootElement;

        Assert.Equal("VisorDatosSIG", root.GetProperty("name").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal("/", root.GetProperty("start_url").GetString());
        Assert.Equal("/", root.GetProperty("scope").GetString());
        Assert.Contains(root.GetProperty("icons").EnumerateArray(), icon => icon.GetProperty("sizes").GetString() == "192x192");
        Assert.Contains(root.GetProperty("icons").EnumerateArray(), icon => icon.GetProperty("purpose").GetString() == "maskable");
    }

    [Fact]
    public void Service_worker_caches_only_public_assets_and_excludes_private_routes()
    {
        var worker = File.ReadAllText(WebAsset("service-worker.js"));

        Assert.Contains("/js/operations.js", worker);
        Assert.Contains("/js/perspective.js", worker);
        Assert.Contains("pathname.startsWith('/api/')", worker);
        Assert.Contains("operaciones|registros", worker);
        Assert.Contains("request.mode === 'navigate'", worker);
    }

    [Fact]
    public void Login_exposes_the_pwa_install_action()
    {
        var login = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "VisorDatosSIG.Web", "Views", "Cuenta", "Login.cshtml"));
        var site = File.ReadAllText(WebAsset("js", "site.js"));

        Assert.Contains("data-pwa-install", login);
        Assert.Contains("Instalar aplicación", login);
        Assert.Contains("beforeinstallprompt", site);
        Assert.Contains("Agregar a pantalla de inicio", site);
    }

    private static string WebAsset(params string[] segments) => Path.Combine([RepositoryRoot(), "src", "VisorDatosSIG.Web", "wwwroot", .. segments]);

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
