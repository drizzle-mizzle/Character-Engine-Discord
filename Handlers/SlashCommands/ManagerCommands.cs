using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.StorageContext;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    public class ManagerCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        private readonly DiscordSocketClient _client;
        private readonly StorageContext _db;

        public ManagerCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _db = services.GetRequiredService<StorageContext>();
        }

        [SlashCommand("clear-webhooks", "Remove all character-webhooks from this server")]
        public async Task ClearServerWebhooks()
        {
            try { await ClearServerWebhooksAsync(); }
            catch (Exception e) { LogException(new[] { e }); }
        }

        [SlashCommand("set-random-reply-chance", "Set random replies chance")]
        public async Task SetRandomReplyChance(float chance)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            channel.RandomReplyChance = chance;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("hunt-user", "Make character respond on messages of certain user")]
        public async Task HuntUser(string webhookId, IUser user, float chanceOfResponse = 100)
        {
            try
            {
                await DeferAsync();

                var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
                var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

                if (characterWebhook is null)
                {
                    await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                    return;
                }

                if (characterWebhook.HuntedUsers.FirstOrDefault(h => h.Id == user.Id) is not null)
                {
                    await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} User is already hunted", Color.Orange));
                    return;
                }

                _db.HuntedUsers.Add(new() { Id = user.Id, Chance = chanceOfResponse, CharacterWebhookId = characterWebhook.Id });
                await _db.SaveChangesAsync();

                await FollowupAsync(embed: InlineEmbed($":ghost: Hunting {user.Mention}", Color.LighterGrey));
            }catch (Exception e) { LogException(new[] { e }); }
        }

        [SlashCommand("stop-hunt-user", "Stop hunting user")]
        public async Task UnhuntUser(string webhookId, IUser user)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            var huntedUser = characterWebhook.HuntedUsers.FirstOrDefault(h => h.Id == user.Id);
            if (huntedUser is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} User is not hunted", Color.Orange));
                return;
            }

            characterWebhook.HuntedUsers.Remove(huntedUser);
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: InlineEmbed($":ghost: {user.Mention} is not hunted anymore", Color.LighterGrey));
        }

        [SlashCommand("reset-character", "Forget all history and start chat from the beginning")]
        public async Task ResetCharacter(string webhookId)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }
            
            if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
            {
                var plusMode = channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
                var caiToken = channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;

                characterWebhook.CaiActiveHistoryId = await _integration.CaiClient!.CreateNewChatAsync(characterWebhook.CharacterId, caiToken, plusMode);
            }
            else
            {
                characterWebhook.OpenAiHistoryMessages.Clear();
            }

            await _db.SaveChangesAsync();
            await FollowupAsync(embed: SuccessEmbed());

            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out var channelWebhook);
            if (channelWebhook is null) return;

            await channelWebhook.SendMessageAsync(characterWebhook.Character.Greeting);
        }

        [SlashCommand("set-default-messages-format", "Change messages format used for all new characters on this server by default")]
        public async Task SetDefaultMessagesFormat(string newFormat)
        {
            await DeferAsync();

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            if (!newFormat.Contains("{{msg}}"))
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Can't set format without a **`{{{{msg}}}}`** placeholder!", Color.Red));
                return;
            }

            guild.GuildMessagesFormat = newFormat;
            await _db.SaveChangesAsync();

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **Success**")
                                          .AddField("New default format:", $"`{newFormat}`")
                                          .AddField("[Example]", $"User message: *`Hello!`*\n" +
                                                                 $"User nickname: `Average AI Enjoyer`\n" +
                                                                 $"Result (what character will see): *`{newFormat.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer")}`*")
                                          .WithColor(Color.Green)
                                          .Build();

            await FollowupAsync(embed: embed);
        }

        [SlashCommand("set-swipes-remove-delay", "Set time after that swipe reaction buttons will fade away")]
        public async Task SetSwipeRemoveDelay(int seconds)
        {
            await DeferAsync(ephemeral: true);

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            guild.BtnsRemoveDelay = seconds;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        [SlashCommand("set-server-cai-user-token", "Set default CharacterAI auth token for this server")]
        public async Task SetGuildCaiToken(string token, bool hasCaiPlusSubscription)
        {
            await DeferAsync(ephemeral: true);

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            guild.GuildCaiUserToken = token;
            guild.GuildCaiPlusMode = hasCaiPlusSubscription;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        [SlashCommand("set-server-openai-api-token", "Set default OpenAI api token for this server")]
        public async Task SetGuildOpenAiToken(string token, OpenAiModel gptModel, string? gptReverseProxyEndpoint)
        {
            await DeferAsync(ephemeral: true);

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            if (gptReverseProxyEndpoint is not null)
                guild.GuildOpenAiApiEndpoint = gptReverseProxyEndpoint;

            guild.GuildOpenAiApiToken = token;
            guild.GuildOpenAiModel = gptModel is OpenAiModel.GPT_3_5_turbo ? "gpt-3.5-turbo" : gptModel is OpenAiModel.GPT_4 ? "gpt-4" : null;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        [SlashCommand("say", "Make character say something")]
        public async Task SayAsync(string webhookId, string text)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            var characterWebhook = channel.CharacterWebhooks.FirstOrDefault(c => c.Id == ulong.Parse(webhookId));

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out var channelWebhook);

            if (channelWebhook is null)
            {
                await FollowupAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Webhook not found", Color.Red));
                return;
            }

            await channelWebhook.SendMessageAsync(text);
            await FollowupAsync(embed: SuccessEmbed());
        }


        ////////////////////////////
        //// Main logic section ////
        ////////////////////////////

        private async Task ClearServerWebhooksAsync()
        {
            await DeferAsync();

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
            await FollowupAsync(embed: SuccessEmbed());
        }
    }
}
