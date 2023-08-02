using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;

namespace CharacterEngineDiscord
{
    internal class Program : DiscordService
    {
        static void Main()
            => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            Log("Working directory: ");
            LogYellow(EXE_DIR + '\n');

            if (!File.Exists($"{EXE_DIR}{SC}logs.txt"))
                File.Create($"{EXE_DIR}{SC}logs.txt").Close();

            await SetupDiscordClient();
            await Task.Delay(-1);
        }
    }
}