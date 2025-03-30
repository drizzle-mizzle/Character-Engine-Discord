using CharacterEngineDiscord.Models;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Config;

namespace CharacterEngineDiscord.Migrator;


internal static class Program
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();


    private static async Task Main()
    {
        var nlogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "NLog.config");
        LogManager.Configuration = new XmlLoggingConfiguration(nlogPath);

        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var connectionStringPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "env.connection-string");
        if (!File.Exists(connectionStringPath))
        {
            connectionStringPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "connection-string");
        }

        var connectionString = await File.ReadAllTextAsync(connectionStringPath);

        await using var db = new AppDbContext(connectionString);
        var migrations = (await db.Database.GetPendingMigrationsAsync()).ToArray();

        if (migrations.Length != 0)
        {
            _log.Info($"Applying database migrations:\n{string.Join("\n", migrations)}");

            await db.Database.MigrateAsync();

            _log.Info("Migrations applied");
        }
        else
        {
            _log.Info("No pending migrations");
        }
    }
}



