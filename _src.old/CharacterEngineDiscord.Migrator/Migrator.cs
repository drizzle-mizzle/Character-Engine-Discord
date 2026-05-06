using CharacterEngineDiscord.Models;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Config;

namespace CharacterEngineDiscord.Migrator;


public class Migrator
{
    private readonly Logger _log = LogManager.GetCurrentClassLogger();

    private Migrator() { }


    public static void Run(string connectionString)
        => new Migrator().RunAsync(connectionString).GetAwaiter().GetResult();


    private async Task RunAsync(string connectionString)
    {
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



