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
            SetEnvs();
            AppLaunchLog();
            await SetupDiscordClient();
            await Task.Delay(-1);
        }
    }
}