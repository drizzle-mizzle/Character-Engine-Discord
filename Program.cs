using CharacterEngineDiscord.Services;
using Microsoft.EntityFrameworkCore;
using static CharacterEngineDiscord.Services.CommonService;

namespace CharacterEngineDiscord
{
    internal class Program
    {
        private static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args)
                => File.AppendAllText($"{EXE_DIR}{SC}logs.txt", $"{new string('~', 10)}\n[{DateTime.Now:u}] {args.ExceptionObject}\n");

            using (var db = new StorageContext())
                db.Database.Migrate();

            var bot = new BotService();
            bot.LaunchAsync().Wait();
        }
    }
}