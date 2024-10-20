using CharacterEngine.App.Helpers;
using CharacterEngineDiscord.Helpers.Common;
using NLog;

namespace CharacterEngine.App
{
    internal static class Program
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();


        private static async Task Main()
        {
            var nlogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Settings\NLog.config");
            LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(nlogPath);

            _log.Info("[ Starting Character Engine ]");

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                _log.Info("[ Character Engine Stopped ]");
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                _log.Error($"Unhandled exception: {e.ExceptionObject}");
            };

            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            await using var db = DatabaseHelper.GetDbContext();
            // await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            // await db.Database.MigrateAsync();

            CharacterEngineBot.Run();
        }
    }
}
