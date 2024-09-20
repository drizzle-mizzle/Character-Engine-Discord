using CharacterEngine.App;
using NLog;

namespace CharacterEngine
{
    internal static class Program
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();


        private static void Main()
        {
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

            CharacterEngineBot.Run();
        }
    }
}
