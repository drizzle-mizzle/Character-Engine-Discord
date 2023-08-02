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
using CharacterEngineDiscord.Models.Database;
using Discord.Webhook;
using static System.Net.Mime.MediaTypeNames;

namespace CharacterEngineDiscord.Handlers.SlashCommands
{
    [RequireManagerAccess]
    public class ManagerCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IntegrationsService _integration;
        //private readonly DiscordSocketClient _client;
        private readonly StorageContext _db;

        public ManagerCommands(IServiceProvider services)
        {
            _integration = services.GetRequiredService<IntegrationsService>();
            //_client = services.GetRequiredService<DiscordSocketClient>();
            _db = new StorageContext();
        }

        [SlashCommand("delete-webhook", "Remove character-webhook from channel")]
        public async Task DeleteWebhook(string webhookIdOrPrefix)
        {
            await DeleteWebhookAsync(webhookIdOrPrefix);
        }

        [SlashCommand("clear-server-webhooks", "Remove all character-webhooks from this server")]
        public async Task ClearServerWebhooks()
        {
            await ClearWebhooksAsync(all: true);
        }

        [SlashCommand("clear-channel-webhooks", "Remove all character-webhooks from this channel")]
        public async Task ClearChannelWebhooks()
        {
            await ClearWebhooksAsync(all: false);
        }

        //[SlashCommand("set-random-reply-chance", "Set random replies chance for this channel")]
        //public async Task SetRandomReplyChance(float chance)
        //{
        //    await DeferAsync();

        //    var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
        //    channel.RandomReplyChance = chance;
        //    await _db.SaveChangesAsync();

        //    await FollowupAsync(embed: SuccessEmbed());
        //}

        [SlashCommand("hunt-user", "Make character respond on messages of certain user (or bot)")]
        public async Task HuntUser(string webhookIdOrPrefix, IUser? user = null, string? userId = null, float chanceOfResponse = 100)
        {
            await HuntUserAsync(webhookIdOrPrefix, user, userId, chanceOfResponse);
        }

        [SlashCommand("stop-hunt-user", "Stop hunting user")]
        public async Task UnhuntUser(string webhookIdOrPrefix, IUser? user = null, string? userId = null)
        {
            await UnhuntUserAsync(webhookIdOrPrefix, user, userId);
        }

        [SlashCommand("reset-character", "Forget all history and start chat from the beginning")]
        public async Task ResetCharacter(string webhookIdOrPrefix)
        {
            await ResetCharacterAsync(webhookIdOrPrefix);
        }

        [SlashCommand("set-default-messages-format", "Change messages format used for all new characters on this server by default")]
        public async Task SetDefaultMessagesFormat(string newFormat)
        {
            await DeferAsync();

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            if (!newFormat.Contains("{{msg}}"))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Can't set format without a **`{{{{msg}}}}`** placeholder!".ToInlineEmbed(Color.Red));
                return;
            }

            int refCount = 0;
            if (newFormat.Contains("{{ref_msg_begin}}")) refCount++;
            if (newFormat.Contains("{{ref_msg_text}}")) refCount++;
            if (newFormat.Contains("{{ref_msg_end}}")) refCount++;

            if (refCount != 0 && refCount != 3)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong `ref_msg` placeholder format!".ToInlineEmbed(Color.Red));
                return;
            }

            guild.GuildMessagesFormat = newFormat;
            await _db.SaveChangesAsync();

            string text = newFormat.Replace("{{msg}}", "Hello!").Replace("{{user}}", "Average AI Enjoyer");

            if (refCount == 3)
            {
                text = text.Replace("{{ref_msg_text}}", "Hola").Replace("{{ref_msg_begin}}", "").Replace("{{ref_msg_end}}", "");
            }

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **Success**").WithColor(Color.Green)
                                          .AddField("New default format:", $"`{newFormat}`")
                                          .AddField("Example", $"User message: *`Hello!`*\n" +
                                                               $"User nickname: `Average AI Enjoyer`\n" +
                                                               $"Referenced message: *`Hola`*\n" +
                                                               $"Result (what character will see): *`{text}`*");

            await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("set-swipes-remove-delay", "Set time after that swipe reaction buttons will fade away on this server")]
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
        public async Task SetGuildOpenAiToken(string token, OpenAiModel gptModel, string? reverseProxyEndpoint = null)
        {
            await DeferAsync(ephemeral: true);

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            if (reverseProxyEndpoint is not null)
                guild.GuildOpenAiApiEndpoint = reverseProxyEndpoint;

            guild.GuildOpenAiApiToken = token;
            guild.GuildOpenAiModel = gptModel is OpenAiModel.GPT_3_5_turbo ? "gpt-3.5-turbo" : gptModel is OpenAiModel.GPT_4 ? "gpt-4" : null;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed(), ephemeral: true);
        }

        [SlashCommand("say", "Make character say something")]
        public async Task SayAsync(string webhookIdOrPrefix, string text)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            _integration.WebhookClients.TryGetValue(characterWebhook.Id, out var webhookClient);

            if (webhookClient is null)
            {
                webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
                _integration.WebhookClients.Add(characterWebhook.Id, webhookClient);
            }

            await webhookClient.SendMessageAsync(text);
            await FollowupAsync(embed: SuccessEmbed());
        }


          ////////////////////
         //// Long stuff ////
        ////////////////////

        private async Task DeleteWebhookAsync(string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            var discordWebhook = await ((SocketTextChannel)Context.Channel).GetWebhookAsync(characterWebhook.Id);
            await discordWebhook.DeleteAsync();

            _db.CharacterWebhooks.Remove(characterWebhook);
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        private async Task ClearWebhooksAsync(bool all)
        {
            await DeferAsync();

            IReadOnlyCollection<IWebhook> discordWebhooks;
            List<CharacterWebhook> trackedWebhooks;

            if (all)
            {
                discordWebhooks = await Context.Guild.GetWebhooksAsync();
                trackedWebhooks = (from guild in (from guilds in _db.Guilds where guilds.Id == Context.Guild.Id select guilds)
                                   join channel in _db.Channels on guild.Id equals channel.Guild.Id
                                   join webhook in _db.CharacterWebhooks on channel.Id equals webhook.Channel.Id
                                   select webhook).ToList();
            }
            else
            { 
                discordWebhooks = await ((SocketTextChannel)Context.Channel).GetWebhooksAsync();
                trackedWebhooks = (from channel in (from channels in _db.Channels where channels.Id == Context.Channel.Id select channels)
                                   join webhook in _db.CharacterWebhooks on channel.Id equals webhook.Channel.Id
                                   select webhook).ToList();
            }

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

        private async Task ResetCharacterAsync(string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
            {
                var plusMode = characterWebhook.Channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
                var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;

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

        private async Task HuntUserAsync(string webhookIdOrPrefix, IUser? user, string? userIdOrCharPrefix, float chanceOfResponse)
        {
            await DeferAsync();

            if (user is null && string.IsNullOrWhiteSpace(userIdOrCharPrefix))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Specify user or user ID".ToInlineEmbed(Color.Red));
                return;
            }

            var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            string? username = null;
            ulong? userToHuntId = user?.Id;
            if (userToHuntId is null)
            {
                bool isId = ulong.TryParse(userIdOrCharPrefix!.Trim(), out ulong userId);
                if (isId)
                {
                    userToHuntId = userId;
                    username = userId.ToString();
                }
                else
                {
                    var cwToHunt = characterWebhook.Channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == userIdOrCharPrefix.Trim());
                    userToHuntId = cwToHunt?.Id;
                    username = cwToHunt?.Character.Name;
                }
            }

            if (userToHuntId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User or character-webhook was not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.HuntedUsers.Any(h => h.Id == userToHuntId))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is already hunted".ToInlineEmbed(Color.Orange));
                return;
            }

            _db.HuntedUsers.Add(new() { Id = (ulong)userToHuntId, Chance = chanceOfResponse, CharacterWebhookId = characterWebhook.Id });
            await _db.SaveChangesAsync();

            username ??= user?.Mention;
            await FollowupAsync(embed: $":ghost: **{characterWebhook.Character.Name}** hunting **{username}**".ToInlineEmbed(Color.LighterGrey));
        }

        private async Task UnhuntUserAsync(string webhookIdOrPrefix, IUser? user, string? userIdOrCharPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            string? username = null;
            ulong? huntedUserId = user?.Id;
            if (huntedUserId is null)
            {
                bool isId = ulong.TryParse(userIdOrCharPrefix!.Trim(), out ulong userId);
                if (isId)
                {
                    huntedUserId = userId;
                    username = userId.ToString();
                }
                else
                {
                    var cwToUnhunt = characterWebhook.Channel.CharacterWebhooks.FirstOrDefault(c => c.CallPrefix.Trim() == userIdOrCharPrefix.Trim());
                    huntedUserId = cwToUnhunt?.Id;
                    username = cwToUnhunt?.Character.Name;
                }
            }

            if (huntedUserId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User or character-webhook was not found".ToInlineEmbed(Color.Red));
                return;
            }

            var huntedUser = characterWebhook.HuntedUsers.FirstOrDefault(h => h.Id == huntedUserId);

            if (huntedUser is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is not hunted".ToInlineEmbed(Color.Orange));
                return;
            }

            characterWebhook.HuntedUsers.Remove(huntedUser);
            await _db.SaveChangesAsync();

            username ??= user?.Mention;
            await FollowupAsync(embed: $":ghost: **{username}** is not hunted anymore".ToInlineEmbed(Color.LighterGrey));
        }

    }
}
