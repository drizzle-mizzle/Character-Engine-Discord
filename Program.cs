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
            Environment.SetEnvironmentVariable("RUNNING", "!", EnvironmentVariableTarget.Process);

            Log("Working directory: ");
            LogYellow(EXE_DIR + '\n');

            await SetupDiscordClient();
            await Task.Delay(-1);
        }
    }
}