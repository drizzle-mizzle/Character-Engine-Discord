using CharacterEngine.App;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Config;
using CharacterEngineDiscord.Domain.Models.Db;

namespace CharacterEngine
{
    internal static class Program
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();


        private static async Task Main(string[] args)
        {
            var nlogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "NLog.config");
            LogManager.Configuration = new XmlLoggingConfiguration(nlogPath);

            _log.Info("[ Starting Character Engine ]");

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                _log.Info("[ Character Engine Stopped ]");
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                _log.Error($"Unhandled exception: {sender}\n{e.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                if (e.Exception.InnerException is not UserFriendlyException)
                {
                    _log.Error($"Unobserved task exception: {sender}\n{e.Exception}");
                }
            };

            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            BotConfig.Initialize();

            if (args.Contains("--migrate"))
            {
                await using var db = DatabaseHelper.GetDbContext();
                var migrations = (await db.Database.GetPendingMigrationsAsync()).ToArray();

                if (migrations.Length != 0)
                {
                    _log.Info($"Applying database migrations:\n{string.Join("\n", migrations)}");

                    await db.Database.MigrateAsync();

                    _log.Info("Migrations applied");
                }
            }

            MetricsWriter.Create(MetricType.ApplicationLaunch);

            await WatchDog.RunAsync();
            await CharacterEngineBot.RunAsync();
        }

    }
}
