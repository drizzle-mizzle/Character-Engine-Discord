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
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var sw = File.AppendText($"{EXE_DIR}{SC}logs.txt");
                string text = $"{new string('~', Console.WindowWidth)}\n" +
                              $"Sender: {s.GetType()}\n" +
                              $"Error:\n{(Exception)args.ExceptionObject}";
                sw.WriteLine(text);
                sw.Close();
            };

            Log("Working directory: ");
            LogYellow(EXE_DIR + '\n');
            CreateLogsFile();
            
            await SetupDiscordClient();
            await Task.Delay(-1);
        }

        private static void CreateLogsFile()
        {
            string logstxt = $"{EXE_DIR}{SC}logs.txt";
            if (File.Exists(logstxt)) return;
            else File.Create(logstxt).Close();
        }
    }
}