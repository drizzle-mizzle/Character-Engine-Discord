using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;


namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    public class PerServerCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly InteractionService _interactions;
        private readonly StorageContext _db;

        public PerServerCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _interactions = services.GetRequiredService<InteractionService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("clear-slash-commands", "Remove all bot slash commands")]
        public async Task ClearSlashCommandsAsync()
        {
            await ((SocketGuild)Context.Guild).DeleteApplicationCommandsAsync();
            await RespondAsync(embed: SuccessMsg());
        }

        [SlashCommand("clear-all-webhooks", "Remove all character-webhooks from this server")]
        public async Task ClearServerWebhooksAsync()
        {
            try
            {
                var discordWebhooks = await Context.Guild.GetWebhooksAsync();
                var trackedWebhooks = (from guild in (from guilds in _db.Guilds where guilds.Id == Context.Guild.Id select guilds)
                                       join channel in _db.Channels on guild.Id equals channel.Guild.Id
                                       join webhook in _db.CharacterWebhooks on channel.Id equals webhook.Channel.Id
                                       select webhook).ToList();
                var trackedWebhookIds = trackedWebhooks.Select(w => w.Id).ToList();

                foreach (var tw in trackedWebhooks)
                {
                    var discordWebhook = discordWebhooks.FirstOrDefault(dw => dw.Id == tw.Id);
                    _ = discordWebhook?.DeleteAsync();
                    _db.CharacterWebhooks.Remove(tw);
                }

                await _db.SaveChangesAsync();
                await RespondAsync(embed: SuccessMsg());
            }
            catch (Exception e) { LogException(new[] { e }); }
        }
    }
}
