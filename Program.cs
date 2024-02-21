using CharacterEngineDiscord.Services;
using NLog.Config;
using NLog.Targets;
using NLog;
using Tmds.Utils;
using static CharacterEngineDiscord.Services.CommonService;

namespace CharacterEngineDiscord
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            if (ExecFunction.IsExecFunctionCommand(args))
                return ExecFunction.Program.Main(args);

            AppDomain.CurrentDomain.UnhandledException += (_, e) => File.AppendAllText($"{EXE_DIR}{SC}wtf_log.txt", $"{new string('~', 10)}\n[{DateTime.Now:u}] {e.ExceptionObject}\n");
            
            if (args.Contains("-puplog"))
                Environment.SetEnvironmentVariable("PUPLOG", "1");

            var config = new LoggingConfiguration();
            var fileTarget = new FileTarget
            {
                Name = "file",
                Layout = "${longdate} | ${message}",
                FileName = "logs.txt"
            };
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, fileTarget, "*");
            LogManager.Configuration = config;

            var bot = new BotService();
            bot.LaunchAsync(args.Contains("-no-reg")).Wait();

            return 0;
        }
    }
}