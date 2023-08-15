using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Services;
using System.Diagnostics;
using static CharacterEngineDiscord.Services.CommonService;

namespace CharacterEngineDiscord
{
    internal class Program : DiscordService
    {
        static void Main()
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            AppDomain.CurrentDomain.UnhandledException += RestartApplication;
            Log("Working directory: ");
            LogYellow(EXE_DIR + '\n');
            CreateLogsFile();
            
            await SetupDiscordClient();
            await Task.Delay(-1);
        }

        private static void CreateLogsFile()
        {
            if (ConfigFile.LogFileEnabled.Value.ToBool())
            {
                string logstxt = $"{EXE_DIR}{SC}logs.txt";
                if (File.Exists(logstxt)) return;
                else File.Create(logstxt).Close();
            }
        }

        private static void RestartApplication(object sender, UnhandledExceptionEventArgs e)
        {
            string precompiledPath = $"{EXE_DIR}{SC}Character-Engine-Discord";

            bool isPrecompiledWindows = File.Exists(precompiledPath + ".exe");
            if (isPrecompiledWindows)
            {
                Process.Start(precompiledPath + ".exe");
                return;
            }

            bool isPrecompiledLinux = File.Exists(precompiledPath);
            if (isPrecompiledLinux)
            {
                Process.Start(precompiledPath);
                return;
            }

            string buildPath = $"{EXE_DIR}{SC}bin{SC}Debug{SC}net7.0";
            string? os = Directory.GetDirectories(buildPath).FirstOrDefault();
            if (os is null) return;

            buildPath += $"{SC}{os}{SC}Character-Engine-Discord";

            bool isBuiltWindows = File.Exists(buildPath + ".exe");
            if (isBuiltWindows)
            {
                Process.Start(buildPath + ".exe");
                return;
            }

            bool isBuiltLinux = File.Exists(buildPath);
            if (isBuiltLinux)
            {
                Process.Start(buildPath);
                return;
            }
        }
    }
}