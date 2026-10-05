using System.Security.Cryptography;
using VisorDatosSIG.Infrastructure.Authentication;
using VisorDatosSIG.Infrastructure.Shapefiles;

namespace VisorDatosSIG.Tests;

public sealed class AuthenticationAndShapefileTests
{
    [Fact]
    public void Pbkdf2Sha256_rejects_an_incorrect_password()
    {
        var salt = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var hash = Rfc2898DeriveBytes.Pbkdf2("correct-password", salt, 100_000, HashAlgorithmName.SHA256, 32);

        Assert.True(SqlAuthenticationService.VerifyPassword("correct-password", salt, hash, 100_000));
        Assert.False(SqlAuthenticationService.VerifyPassword("incorrect-password", salt, hash, 100_000));
    }

    [Fact]
    public void Pbkdf2Sha256_generated_hash_verifies_its_password()
    {
        var password = SqlAuthenticationService.HashPassword("a-safe-temporary-password");

        Assert.True(SqlAuthenticationService.VerifyPassword("a-safe-temporary-password", password.Salt, password.Hash, password.Iterations));
    }

    [Fact]
    public void Diagnostics_reports_missing_components_without_reading_a_shapefile()
    {
        var service = new ShapefileDiagnosticService(Path.GetTempPath());
        var diagnostics = service.GetDiagnostics(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".shp"));

        Assert.False(diagnostics.HasShp);
        Assert.False(diagnostics.HasShx);
        Assert.False(diagnostics.HasDbf);
        Assert.False(diagnostics.HasPrj);
        Assert.False(diagnostics.IsPrjValid);
    }

    [Fact]
    public void Unknown_official_layer_is_rejected()
    {
        var service = new ShapefileDiagnosticService();

        Assert.Throws<KeyNotFoundException>(() => service.GetLayerPath("arbitrary-layer"));
    }

    [Fact]
    public void Official_layers_are_recognized_with_required_components_and_wgs84_prj()
    {
        var service = new ShapefileDiagnosticService();

        Assert.Equal(["manzanas", "lotes", "codigos-fijos", "vias"], service.GetOfficialLayers().Select(layer => layer.Key));
        foreach (var layer in service.GetOfficialLayers())
        {
            var diagnostics = service.GetDiagnosticsForLayer(layer.Key);
            Assert.True(diagnostics.HasShp);
            Assert.True(diagnostics.HasShx);
            Assert.True(diagnostics.HasDbf);
            Assert.True(diagnostics.HasPrj);
            Assert.True(diagnostics.IsPrjValid);
        }
    }
}
