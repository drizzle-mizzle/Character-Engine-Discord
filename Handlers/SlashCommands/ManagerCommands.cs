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

        [SlashCommand("copy-character-from-channel", "As it says")]
        public async Task CopyCharacter(IChannel channel, string webhookIdOrPrefix)
        {
            await CopyCharacterAsync(channel, webhookIdOrPrefix);
        }

        [SlashCommand("set-channel-random-reply-chance", "Set random character replies chance for this channel")]
        public async Task SetChannelRandomReplyChance(float chance)
        {
            await DeferAsync();

            var channel = await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id, _db);
            channel.RandomReplyChance = chance;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("hunt-user", "Make character respond on messages of certain user (or bot)")]
        public async Task HuntUser(string webhookIdOrPrefix, IUser? user = null, string? userIdOrCharacterPrefix = null, float chanceOfResponse = 100)
        {
            await HuntUserAsync(webhookIdOrPrefix, user, userIdOrCharacterPrefix, chanceOfResponse);
        }

        [SlashCommand("stop-hunt-user", "Stop hunting user")]
        public async Task UnhuntUser(string webhookIdOrPrefix, IUser? user = null, string? userIdOrCharacterPrefix = null)
        {
            await UnhuntUserAsync(webhookIdOrPrefix, user, userIdOrCharacterPrefix);
        }

        [SlashCommand("reset-character", "Forget all history and start chat from the beginning")]
        public async Task ResetCharacter(string webhookIdOrPrefix)
        {
            await ResetCharacterAsync(webhookIdOrPrefix);
        }

        [SlashCommand("set-server-messages-format", "Change messages format used for all new characters on this server by default")]
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
                text = text.Replace("{{ref_msg_text}}", "Hola")
                           .Replace("{{ref_msg_begin}}", "")
                           .Replace("{{ref_msg_end}}", "")
                           .Replace("{{ref_msg_user}}", "Dude")
                           .Replace("\\n", "\n");
            }

            var embed = new EmbedBuilder().WithTitle($"{OK_SIGN_DISCORD} **Success**").WithColor(Color.Green)
                                          .AddField("New default format:", $"`{newFormat}`")
                                          .AddField("Example", $"User message: *`Hello!`*\n" +
                                                               $"User nickname: `Average AI Enjoyer`\n" +
                                                               $"Referenced message: *`Hola`* from user *`Dude`*\n" +
                                                               $"Result (what character will see): *`{text}`*");

            await FollowupAsync(embed: embed.Build());
        }

        [SlashCommand("drop-server-messages-format", "Drop default messages format for this server")]
        public async Task DropGuildMessagesFormat()
        {
            await DeferAsync(ephemeral: true);

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            guild.GuildMessagesFormat = null;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("set-server-jailbreak-prompt", "Change messages format used for all new characters on this server by default")]
        public async Task SetServerDefaultPrompt()
        {
            var modal = new ModalBuilder().WithTitle($"Update jailbreak prompt for this server")
                                          .WithCustomId($"guild~{Context.Guild.Id}")
                                          .AddTextInput("New jailbreak prompt", "new-prompt", TextInputStyle.Paragraph)
                                          .Build();
            await RespondWithModalAsync(modal);
        }

        [SlashCommand("drop-server-jailbreak-prompt", "Drop default jailbreak prompt for this server")]
        public async Task DropGuildPrompt()
        {
            await DeferAsync(ephemeral: true);

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            guild.GuildJailbreakPrompt = null;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
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

        [SlashCommand("drop-server-cai-user-token", "Drop default CharacterAI auth token for this server")]
        public async Task DropGuildCaiToken()
        {
            await DeferAsync(ephemeral: true);

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            guild.GuildCaiUserToken = null;
            guild.GuildCaiPlusMode = null;
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
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

        [SlashCommand("drop-server-openai-api-token", "Drop default OpenAI api token for this server")]
        public async Task DropGuildOpenAiToken()
        {
            await DeferAsync(ephemeral: true);

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id, _db);

            guild.GuildOpenAiApiEndpoint = null;
            guild.GuildOpenAiApiToken = null;
            guild.GuildOpenAiModel = null;

            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("say", "Make character say something")]
        public async Task SayAsync(string webhookIdOrPrefix, string text)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            var webhookClient = _integration.GetWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            if (webhookClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Something went wrong...".ToInlineEmbed(Color.Red));
                return;
            }

            await webhookClient.SendMessageAsync(text);
            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("block-user", "Make characters ignore certain user on this server.")]
        public async Task ServerBlockUser(IUser? user = null, string? userId = null, [Summary(description: "Don't specify hours to block forever")]int hours = 0)
        {
            await DeferAsync();

            if (user is null && userId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user or user-ID".ToInlineEmbed(Color.Red));
                return;
            }

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id);

            ulong uUserId;
            if (user is null)
            {
                bool ok = ulong.TryParse(userId, out uUserId);

                if (!ok)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red));
                    return;
                }
            }
            else
            {
                uUserId = user!.Id;
            }

            if (guild.BlockedUsers.Any(bu => bu.Id == uUserId))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is already blocked".ToInlineEmbed(Color.Red));
                return;
            }

            await _db.BlockedUsers.AddAsync(new() { Id = uUserId, From = DateTime.UtcNow, Hours = hours, GuildId = guild.Id });
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }

        [SlashCommand("unblock-user", "---")]
        public async Task ServerUnblockUser(IUser? user = null, string? userId = null)
        {
            await DeferAsync();

            if (user is null && userId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user or user-ID".ToInlineEmbed(Color.Red));
                return;
            }

            ulong uUserId;
            if (user is null)
            {
                bool ok = ulong.TryParse(userId, out uUserId);

                if (!ok)
                {
                    await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Wrong user ID".ToInlineEmbed(Color.Red));
                    return;
                }
            }
            else
            {
                uUserId = user!.Id;
            }

            var guild = await FindOrStartTrackingGuildAsync(Context.Guild.Id);

            var blockedUser = guild.BlockedUsers.FirstOrDefault(bu => bu.Id == uUserId);
            if (blockedUser is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User not found".ToInlineEmbed(Color.Red));
                return;
            }

            _db.BlockedUsers.Remove(blockedUser);
            await _db.SaveChangesAsync();

            await FollowupAsync(embed: SuccessEmbed());
        }


        ////////////////////
        //// Long stuff ////
        ////////////////////

        private async Task DeleteWebhookAsync(string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (Context.Channel is not ITextChannel textChannel)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Something went wrong".ToInlineEmbed(Color.Red));
                return;
            }

            try
            {
                var discordWebhook = await textChannel.GetWebhookAsync(characterWebhook.Id);
                await discordWebhook.DeleteAsync();
            }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to delete webhook: `{e.Message}`".ToInlineEmbed(Color.Red));
                return;
            }

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

            if (_integration.CaiClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} CharacterAI integration is disabled".ToInlineEmbed(Color.Red));
                return;
            }

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.IntegrationType is IntegrationType.CharacterAI)
            {
                var plusMode = characterWebhook.Channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
                var caiToken = characterWebhook.Channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
                var newHisoryId = await _integration.CaiClient.CreateNewChatAsync(characterWebhook.CharacterId, caiToken, plusMode);
                characterWebhook.CaiActiveHistoryId = newHisoryId;
            }
            else
            {
                characterWebhook.OpenAiHistoryMessages.Clear();
                var firstGreetingMessage = new OpenAiHistoryMessage() { CharacterWebhookId = characterWebhook.Id, Content = characterWebhook.Character.Greeting, Role = "assistant" };
                await _db.OpenAiHistoryMessages.AddAsync(firstGreetingMessage);
            }

            await _db.SaveChangesAsync();
            await FollowupAsync(embed: SuccessEmbed());

            var webhookClient = _integration.GetWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
            if (webhookClient is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to send character greeting message".ToInlineEmbed(Color.Red));
                return;
            }

            string characterMessage = $"{Context.User.Mention} {characterWebhook.Character.Greeting.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{(Context.User as SocketGuildUser)?.GetBestName()}**")}";
            if (characterMessage.Length > 2000) characterMessage = characterMessage[0..1994] + "[...]";

            await webhookClient.SendMessageAsync(characterMessage);
        }

        private async Task CopyCharacterAsync(IChannel iChannel, string webhookIdOrPrefix)
        {
            await DeferAsync();

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, iChannel.Id, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (Context.Channel is not IIntegrationChannel discordChannel)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to copy the character".ToInlineEmbed(Color.Red));
                return;
            }

            string name = characterWebhook.Character.Name.ToLower().Contains("discord") ? characterWebhook.Character.Name.Replace('o', 'о').Replace('c', 'с') : characterWebhook.Character.Name;
            var image = await TryDownloadImgAsync(characterWebhook.Character.AvatarUrl, _integration.HttpClient);
            image ??= new MemoryStream(File.ReadAllBytes($"{EXE_DIR}{SC}storage{SC}default_avatar.png"));

            IWebhook channelWebhook;
            try
            {
                channelWebhook = await discordChannel.CreateWebhookAsync(name, image);
            }
            catch (Exception e)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to create webhook: {e.Message}".ToInlineEmbed(Color.Red));
                return;
            }

            try
            {
                _db.CharacterWebhooks.Add(new()
                {
                    Id = channelWebhook.Id,
                    WebhookToken = channelWebhook.Token,
                    ChannelId = (await FindOrStartTrackingChannelAsync(Context.Channel.Id, Context.Guild.Id)).Id,
                    LastCallTime = DateTime.Now,
                    ReplyChance = 0,
                    ResponseDelay = 1,
                    CallPrefix = characterWebhook.CallPrefix,
                    CharacterId = characterWebhook.CharacterId,
                    CrutchEnabled = characterWebhook.CrutchEnabled,
                    IntegrationType = characterWebhook.IntegrationType,
                    MessagesFormat = characterWebhook.MessagesFormat,
                    OpenAiFreqPenalty = characterWebhook.OpenAiFreqPenalty,
                    OpenAiMaxTokens = characterWebhook.OpenAiMaxTokens,
                    OpenAiModel = characterWebhook.OpenAiModel,
                    OpenAiPresencePenalty = characterWebhook.OpenAiPresencePenalty,
                    OpenAiTemperature = characterWebhook.OpenAiTemperature,
                    ReferencesEnabled = characterWebhook.ReferencesEnabled,
                    SwipesEnabled = characterWebhook.SwipesEnabled,
                    UniversalJailbreakPrompt = characterWebhook.UniversalJailbreakPrompt,
                    PersonalCaiUserAuthToken = characterWebhook.PersonalCaiUserAuthToken,
                    PersonalOpenAiApiEndpoint = characterWebhook.PersonalOpenAiApiEndpoint,
                    PersonalOpenAiApiToken = characterWebhook.PersonalOpenAiApiToken,
                    CaiActiveHistoryId = null
                });

                if (characterWebhook.IntegrationType is not IntegrationType.CharacterAI)
                    _db.OpenAiHistoryMessages.Add(new() { CharacterWebhookId = channelWebhook.Id, Content = characterWebhook.Character.Greeting, Role = "assistant" });

                await _db.SaveChangesAsync();

                var webhookClient = new DiscordWebhookClient(channelWebhook.Id, channelWebhook.Token);
                _integration.WebhookClients.TryAdd(channelWebhook.Id, webhookClient);

                string characterMessage = $"{Context.User.Mention} {characterWebhook.Character.Greeting.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{(Context.User as SocketGuildUser)?.GetBestName()}**")}";
                if (characterMessage.Length > 2000) characterMessage = characterMessage[0..1994] + "[...]";

                await FollowupAsync(embed: SuccessEmbed());
                await webhookClient.SendMessageAsync(characterMessage);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                await TryToReportInLogsChannel(_client, "Exception", "Failed to spawn character", e.ToString(), Color.Red, true);
                await channelWebhook.DeleteAsync();
            }
        }

        private async Task HuntUserAsync(string webhookIdOrPrefix, IUser? user, string? userIdOrCharacterPrefix, float chanceOfResponse)
        {
            await DeferAsync();

            if (user is null && string.IsNullOrWhiteSpace(userIdOrCharacterPrefix))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Specify user or user ID".ToInlineEmbed(Color.Red));
                return;
            }

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            string? username = user?.Mention;
            ulong? userToHuntId = user?.Id;

            if (userToHuntId is null)
            {
                bool isId = ulong.TryParse(userIdOrCharacterPrefix!.Trim(), out ulong userId);
                if (isId)
                {
                    userToHuntId = userId;
                    username = userId.ToString();
                }
                else
                {
                    var characterToHunt = await TryToFindCharacterWebhookInChannelAsync(userIdOrCharacterPrefix, Context, _db);
                    userToHuntId = characterToHunt?.Id;
                    username = characterToHunt?.Character.Name;
                }
            }

            if (userToHuntId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User or character-webhook was not found".ToInlineEmbed(Color.Red));
                return;
            }

            if (characterWebhook.HuntedUsers.Any(h => h.UserId == userToHuntId))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is already hunted".ToInlineEmbed(Color.Orange));
                return;
            }

            await _db.HuntedUsers.AddAsync(new() { UserId = (ulong)userToHuntId, Chance = chanceOfResponse, CharacterWebhookId = characterWebhook.Id });
            await _db.SaveChangesAsync();

            username ??= user?.Mention;
            await FollowupAsync(embed: $":ghost: **{characterWebhook.Character.Name}** hunting **{username}**".ToInlineEmbed(Color.LighterGrey, false));
        }

        private async Task UnhuntUserAsync(string webhookIdOrPrefix, IUser? user, string? userIdOrCharacterPrefix)
        {
            await DeferAsync();

            if (user is null && string.IsNullOrWhiteSpace(userIdOrCharacterPrefix))
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Specify user or user ID".ToInlineEmbed(Color.Red));
                return;
            }

            var characterWebhook = await TryToFindCharacterWebhookInChannelAsync(webhookIdOrPrefix, Context, _db);

            if (characterWebhook is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Webhook not found".ToInlineEmbed(Color.Red));
                return;
            }

            string? username = user?.Mention;
            ulong? huntedUserId = user?.Id;

            if (huntedUserId is null)
            {
                bool isId = ulong.TryParse(userIdOrCharacterPrefix!.Trim(), out ulong userId);
                if (isId)
                {
                    huntedUserId = userId;
                    username = userId.ToString();
                }
                else
                {
                    var characterToUnhunt = await TryToFindCharacterWebhookInChannelAsync(userIdOrCharacterPrefix, Context, _db);
                    huntedUserId = characterToUnhunt?.Id;
                    username = characterToUnhunt?.Character.Name;
                }
            }

            if (huntedUserId is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User or character-webhook was not found".ToInlineEmbed(Color.Red));
                return;
            }

            var huntedUser = characterWebhook.HuntedUsers.FirstOrDefault(h => h.UserId == huntedUserId);

            if (huntedUser is null)
            {
                await FollowupAsync(embed: $"{WARN_SIGN_DISCORD} User is not hunted".ToInlineEmbed(Color.Orange));
                return;
            }

            characterWebhook.HuntedUsers.Remove(huntedUser);
            await _db.SaveChangesAsync();

            username ??= user?.Mention;
            await FollowupAsync(embed: $":ghost: **{characterWebhook.Character.Name}** is not hunting **{username}** anymore".ToInlineEmbed(Color.LighterGrey, false));
        }

    }
}
