using Microsoft.EntityFrameworkCore;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Infrastructure.Shapefiles;

namespace VisorDatosSIG.Migrador;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static async Task Main(string[] args)
    {
        if (args.Length > 0)
        {
            await RunMigrationCommandAsync(args);
            return;
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.Run(new Form1());
    }

    private static async Task RunMigrationCommandAsync(string[] args)
    {
        var command = args[0].ToLowerInvariant();
        if (command is not "--validate" and not "--migrate")
        {
            Console.Error.WriteLine("Uso: --validate|--migrate [--append] [--connection <cadena>]");
            Environment.ExitCode = 2;
            return;
        }

        var connectionIndex = Array.FindIndex(args, value => value.Equals("--connection", StringComparison.OrdinalIgnoreCase));
        var connection = connectionIndex >= 0 && connectionIndex + 1 < args.Length
            ? args[connectionIndex + 1]
            : Form1.DefaultConnectionString;
        var mode = args.Any(value => value.Equals("--append", StringComparison.OrdinalIgnoreCase)) ? ShapefileMigrationMode.Append : ShapefileMigrationMode.Replace;
        var options = new DbContextOptionsBuilder<VisorDatosSigDbContext>().UseSqlServer(connection, sql => sql.UseNetTopologySuite()).Options;
        await using var dbContext = new VisorDatosSigDbContext(options);
        var migration = new ShapefileSqlMigrationService(dbContext, new ShapefileDiagnosticService());
        var result = command == "--validate" ? await migration.ValidateAsync(mode) : await migration.MigrateAsync(mode);
        foreach (var layer in result.Layers)
        {
            Console.WriteLine($"{layer.Layer}: source={layer.SourceRecords}, inserted={layer.Inserted}, skippedEmpty={layer.SkippedEmpty}, skippedInvalid={layer.SkippedInvalid}, database={layer.DatabaseRecords}");
        }
        foreach (var error in result.Errors) Console.Error.WriteLine(error);
        Environment.ExitCode = result.IsValid ? 0 : 1;
    }
}
