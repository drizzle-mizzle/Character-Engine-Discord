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
        private readonly StorageContext _db;

        public PerServerCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _db = services.GetRequiredService<StorageContext>();
        }

        public enum OpenAiModel
        {
            [ChoiceDisplay("gpt-3.5-turbo")]
            GPT_3_5_turbo,

            [ChoiceDisplay("gpt-4")]
            GPT_4
        }

        [SlashCommand("clear-webhooks", "Remove all character-webhooks from this server")]
        public async Task ClearServerWebhooks()
        {
            var user = Context.User as SocketGuildUser;
            if (user.IsCharManager() || user.IsServerOwner() || user.IsHoster())
            {
                try { await ClearServerWebhooksAsync(); }
                catch (Exception e) { LogException(new[] { e }); }
            }
            else
                await Context.SendNoPowerFileAsync();
        }

        [SlashCommand("set-server-cai-user-token", "Set default CharacterAI auth token for this server")]
        public async Task SetDefaultGuildCaiToken(string token, bool hasCaiPlusSubscription)
        {
            var user = Context.User as SocketGuildUser;
            if (user.IsCharManager() || user.IsServerOwner() || user.IsHoster())
            {
                try { await SetDefaultGuildCaiTokenAsync(token, hasCaiPlusSubscription); }
                catch (Exception e) { LogException(new[] { e }); }
            }
            else
                await Context.SendNoPowerFileAsync();
        }

        [SlashCommand("set-server-openai-api-token", "Set default OpenAI api token for this server")]
        public async Task SetDefaultGuildOpenAiToken(string token, OpenAiModel gptModel)
        {
            var user = Context.User as SocketGuildUser;
            if (user.IsCharManager() || user.IsServerOwner() || user.IsHoster())
            {
                try { await SetDefaultGuildOpenAiTokenAsync(token, gptModel); }
                catch (Exception e) { LogException(new[] { e }); }
            }
            else
                await Context.SendNoPowerFileAsync();
        }


        ////////////////////////////
        //// Main logic section ////
        ////////////////////////////

        private async Task SetDefaultGuildOpenAiTokenAsync(string token, OpenAiModel gptModel)
        {
            var guild = await FindOrStartTrackingGuildAsync((ulong)Context.Interaction.GuildId!, _db);
            if (guild is null)
            {
                await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Failed to update default CharacterAI user auth token.", Color.Red));
                return;
            }

            guild.GuildOpenAiApiToken = token;
            guild.GuildOpenAiModel = gptModel is OpenAiModel.GPT_3_5_turbo ? "gpt-3.5-turbo" : gptModel is OpenAiModel.GPT_4 ? "gpt-4" : null;

            await _db.SaveChangesAsync();
            await RespondAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        private async Task SetDefaultGuildCaiTokenAsync(string token, bool hasCaiPlusSubscription)
        {
            var guild = await FindOrStartTrackingGuildAsync((ulong)Context.Interaction.GuildId!, _db);
            if (guild is null)
            {
                await RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Failed to update default CharacterAI user auth token.", Color.Red));
                return;
            }

            guild.GuildCaiUserToken = token;
            guild.GuildCaiPlusMode = hasCaiPlusSubscription;

            await _db.SaveChangesAsync();
            await RespondAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        private async Task ClearServerWebhooksAsync()
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
            await RespondAsync(embed: SuccessEmbed());
        }
    }
}
