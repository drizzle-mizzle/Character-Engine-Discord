using Discord;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using Discord.WebSocket;


namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    public class OtherCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly DiscordSocketClient _client;
        private readonly StorageContext _db;

        public OtherCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _db = new StorageContext();
        }

        [SlashCommand("ping", "ping")]
        public async Task Ping()
        {
            await RespondAsync(embed: $":ping_pong: Pong! - {_client.Latency} ms".ToInlineEmbed(Color.Red));
        }
    }
}
