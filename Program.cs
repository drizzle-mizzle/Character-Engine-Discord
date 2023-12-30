using CharacterEngineDiscord.Services;
using Microsoft.EntityFrameworkCore;
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
                string logstxt = $"{EXE_DIR}{SC}logs.txt";
                if (!File.Exists(logstxt)) File.Create(logstxt).Close();

                var sw = File.AppendText(logstxt);
                string text = $"{new string('~', 10)}\n" +
                              $"Sender: {s?.GetType()}\n" +
                              $"Error:\n{args?.ExceptionObject}\n";
                sw.WriteLine(text);
                sw.Close();
            };

            using (var db = new StorageContext())
                await db.Database.MigrateAsync();

            await BotLaunchAsync();
            await Task.Delay(-1);
        }
    }
}