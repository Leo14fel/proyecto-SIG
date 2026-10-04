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
