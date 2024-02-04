using CharacterEngineDiscord.Services;
using Microsoft.EntityFrameworkCore;
using static CharacterEngineDiscord.Services.CommonService;

namespace CharacterEngineDiscord
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e)
                => File.AppendAllText($"{EXE_DIR}{SC}logs.txt", $"{new string('~', 10)}\n[{DateTime.Now:u}] {e.ExceptionObject}\n");

            using (var db = new StorageContext())
                db.Database.Migrate();

            var bot = new BotService();
            bot.LaunchAsync(args.Contains("-no-reg")).Wait();
        }
    }
}