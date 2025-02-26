using System.Text;
using System.Text.RegularExpressions;
using CharacterAi.Client.Exceptions;
using CharacterAi.Client.Models.Common;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Abstractions.OpenRouter;
using CharacterEngineDiscord.Domain.Models.Common;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Domain.Models.Db.SpawnedCharacters;
using CharacterEngineDiscord.Modules.Helpers;
using Discord;
using Discord.Net;
using Discord.Webhook;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NLog;
using PhotoSauce.MagicScaler;
using SakuraAi.Client.Exceptions;
using SakuraAi.Client.Models.Common;
using MT = CharacterEngine.App.Helpers.Discord.MessagesTemplates;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;

namespace CharacterEngine.App.Helpers.Discord;


public static class InteractionsHelper
{
    public const string COMMAND_SEPARATOR = "~sep~";

    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly Regex DISCORD_REGEX = new("discord", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);


    public static (bool valid, string? message) ValidateUserFriendlyException(this Exception exception)
    {
        var ie = exception.InnerException;
        if (ie is not null && Check(ie))
        {
            return (true, ie.Message);
        }

        return Check(exception) ? (true, exception.Message) : (false, null);

        bool Check(Exception e)
            => e is UserFriendlyException or SakuraException or CharacterAiException;
    }


    public static (bool valid, string? message) ValidateWebhookException(this Exception exception)
    {
        var ie = exception.InnerException;
        if (ie is not null && Check(ie))
        {
            return (true, ie.Message);
        }

        return Check(exception) ? (true, exception.Message) : (false, null);

        bool Check(Exception e)
            => (e is HttpException or InvalidOperationException)
            && (e.Message.Contains("Unknown Webhook") || e.Message.Contains("Could not find a webhook"));
    }


    public static async Task RespondWithErrorAsync(IDiscordInteraction interaction, Exception e, string traceId)
    {
        var userFriendlyExceptionCheck = e.ValidateUserFriendlyException();

        Embed embed;

        if (userFriendlyExceptionCheck.valid)
        {
            var ufEx = (e as UserFriendlyException ?? e.InnerException as UserFriendlyException)!;

            var message = ufEx.Bold ? $"**{userFriendlyExceptionCheck.message}**" : userFriendlyExceptionCheck.message!;
            if (!(message.StartsWith(MT.X_SIGN_DISCORD) || message.StartsWith(MT.WARN_SIGN_DISCORD)))
            {
                message = $"{MT.X_SIGN_DISCORD} {message}";
            }

            embed = new EmbedBuilder().WithColor(ufEx.Color).WithDescription(message).Build();
        }
        else
        {
            embed = new EmbedBuilder().WithColor(Color.Red)
                                      .WithDescription($"{MT.X_SIGN_DISCORD} **Something went wrong!**")
                                      .WithFooter($"ERROR TRACE ID: {traceId}")
                                      .Build();
        }

        try
        {
            await interaction.RespondAsync(embed: embed);
        }
        catch
        {
            try
            {
                await interaction.FollowupAsync(embed: embed);
            }
            catch
            {
                try
                {
                    await interaction.ModifyOriginalResponseAsync(msg => { msg.Embed = embed; });
                }
                catch
                {
                    try
                    {
                        var channel = (ITextChannel)CharacterEngineBot.DiscordClient.GetChannel((ulong)interaction.ChannelId!);
                        await channel.SendMessageAsync(embed: embed);
                    }
                    catch
                    {
                        // ...but in the end, it doesn't even matter
                    }
                }
            }
        }
    }


    public static async Task SendSakuraAiMailAsync(IDiscordInteraction interaction, string email)
    {
        var sakuraAiModule = MemoryStorage.IntegrationModules.SakuraAiModule;
        var attempt = await sakuraAiModule.SendLoginEmailAsync(email);

        // Respond to user
        var msg = $"{IntegrationType.SakuraAI.GetIcon()} **SakuraAI**\n\n" +
                  $"Confirmation mail was sent to **{email}**. Please check your mailbox and follow further instructions.\n\n" +
                  $"- *It's **highly recommended** to open an [Incognito Tab](https://support.google.com/chrome/answer/95464), before you open a link in the mail.*\n" +
                  $"- *It may take up to a minute for the bot to react on successful confirmation.*\n" +
                  $"- *If you're willing to put this account into several integrations on different servers, **DO NOT USE `/integration create` command again**, it may break existing integration; use `/integration copy` command instead.*";

        await interaction.FollowupAsync(embed: msg.ToInlineEmbed(bold: false, color: Color.Green), ephemeral: true);

        // Update db
        var data = StoredActionsHelper.CreateSakuraAiEnsureLoginData(attempt, (ulong)interaction.ChannelId!, interaction.User.Id);
        var newAction = new StoredAction(StoredActionType.SakuraAiEnsureLogin, data, maxAttemtps: 25);

        await using var db = DatabaseHelper.GetDbContext();
        db.StoredActions.Add(newAction);
        await db.SaveChangesAsync();
    }


    public static async Task SendCharacterAiMailAsync(IDiscordInteraction interaction, string email)
    {
        var caiModule = MemoryStorage.IntegrationModules.CaiModule;

        try
        {
            await caiModule.SendLoginEmailAsync(email);
        }
        catch (CharacterAiException e)
        {
            _log.Error(e.ToString());
            await interaction.FollowupAsync(embed: $"{MT.WARN_SIGN_DISCORD} CharacterAI responded with error:\n```{e.Message}```".ToInlineEmbed(Color.Red), ephemeral: true);

            return;
        }

        var msg = $"{IntegrationType.CharacterAI.GetIcon()} **CharacterAI**\n\n" +
                  $"Sign in mail was sent to **{email}**, please check your mailbox.\nYou should've received a sign in link for CharacterAI in it - **DON'T OPEN IT (!)**, copy it and then paste in `/integration confirm` command.\n" +
                  $"**Example:**\n*`/integration confirm type:CharacterAI data:https://character.ai/login/xxx`*";

        await interaction.FollowupAsync(embed: msg.ToInlineEmbed(bold: false, color: Color.Green), ephemeral: true);
    }


    #region CustomId

    public static string NewCustomId(ModalActionType action, string data)
        => NewCustomId(Guid.NewGuid(), action, data);

    public static string NewCustomId(ModalData modalData)
        => NewCustomId(modalData.Id, modalData.ActionType, modalData.Data);

    public static string NewCustomId(Guid id, ModalActionType action, string data)
        => $"{id}{COMMAND_SEPARATOR}{action}{COMMAND_SEPARATOR}{data}";


    public static ModalData ParseCustomId(string customId)
    {
        var parts = customId.Split(COMMAND_SEPARATOR);
        return new ModalData(Guid.Parse(parts[0]), Enum.Parse<ModalActionType>(parts[1]), parts[2]);
    }

    #endregion


    #region Characters

    public static async Task<ISpawnedCharacter> SpawnCharacterAsync(ulong channelId, CommonCharacter commonCharacter)
    {
        if (CharacterEngineBot.DiscordClient.GetChannel(channelId) is not ITextChannel channel)
        {
            throw new Exception($"Failed to get channel {channelId}");
        }

        var webhook = await CreateDiscordWebhookAsync(channel, commonCharacter.CharacterName, commonCharacter.CharacterImageLink);
        var webhookClient = new DiscordWebhookClient(webhook.Id, webhook.Token);
        MemoryStorage.CachedWebhookClients.Add(webhook.Id, webhookClient);

        ISpawnedCharacter newSpawnedCharacter;
        try
        {
            newSpawnedCharacter = await CreateSpawnedCharacterAsync(commonCharacter, webhook);
        }
        catch
        {
            MemoryStorage.CachedWebhookClients.Remove(webhook.Id);

            try
            {
                await webhookClient.DeleteWebhookAsync();
            }
            catch
            {
                // care not
            }

            throw;
        }

        MemoryStorage.CachedCharacters.Add(newSpawnedCharacter, []);
        MetricsWriter.Create(MetricType.CharacterSpawned, newSpawnedCharacter.Id, $"{newSpawnedCharacter.GetIntegrationType():G} | {newSpawnedCharacter.CharacterName}");
        return newSpawnedCharacter;
    }


    private static readonly Regex FILTER_REGEX = new(@"[^a-zA-Z0-9\s]", RegexOptions.Compiled);
    private static async Task<ISpawnedCharacter> CreateSpawnedCharacterAsync(CommonCharacter commonCharacter, IWebhook webhook)
    {
        var characterName = FILTER_REGEX.Replace(commonCharacter.CharacterName.Trim(), string.Empty);
        if (characterName.Length == 0)
        {
            throw new UserFriendlyException("Invalid character name");
        }

        string callPrefix;
        if (characterName.Contains(' '))
        {
            var split = characterName.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            callPrefix = $"@{split[0][0]}{split[1][0]}";
        }
        else if (characterName.Length > 2)
        {
            callPrefix = $"@{characterName[..2]}";
        }
        else
        {
            callPrefix = $"@{characterName}";
        }

        var guildIntegration = await DatabaseHelper.GetGuildIntegrationAsync((ulong)webhook.GuildId!, commonCharacter.GetIntegrationType());
        ArgumentNullException.ThrowIfNull(guildIntegration);

        var searchModule = commonCharacter.CharacterSourceType is CharacterSourceType cst
                ? cst.GetSearchModule()
                : commonCharacter.IntegrationType.GetSearchModule();

        var characterAdapter = await searchModule.GetCharacterInfoAsync(commonCharacter.CharacterId, guildIntegration);
        var fullCommonCharacter = characterAdapter.ToCommonCharacter();


        await using var db = DatabaseHelper.GetDbContext();
        ISpawnedCharacter newSpawnedCharacter;

        switch (commonCharacter.IntegrationType)
        {
            case IntegrationType.SakuraAI:
            {
                newSpawnedCharacter = db.SakuraAiSpawnedCharacters.Add(new SakuraAiSpawnedCharacter(characterAdapter.GetValue<SakuraCharacter>())).Entity;
                break;
            }
            case IntegrationType.CharacterAI:
            {
                newSpawnedCharacter = db.CaiSpawnedCharacters.Add(new CaiSpawnedCharacter(characterAdapter.GetValue<CaiCharacter>())).Entity;
                break;
            }
            case IntegrationType.OpenRouter:
            {
                var reusableCharacter = characterAdapter.ToReusableCharacter();
                newSpawnedCharacter = db.OpenRouterSpawnedCharacters.Add(new OpenRouterSpawnedCharacter((IOpenRouterIntegration)guildIntegration)
                {
                    AdoptedCharacterSourceType = reusableCharacter.GetCharacterSourceType(),
                    AdoptedCharacterDefinition = reusableCharacter.GetCharacterDefinition(),
                    AdoptedCharacterLink = reusableCharacter.GetCharacterLink(),
                    AdoptedCharacterAuthorLink = reusableCharacter.GetAuthorLink(),
                }).Entity;
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(commonCharacter.IntegrationType));
            }
        }

        newSpawnedCharacter.CharacterId = fullCommonCharacter.CharacterId;
        newSpawnedCharacter.CharacterName = fullCommonCharacter.CharacterName;
        newSpawnedCharacter.CharacterFirstMessage = fullCommonCharacter.CharacterFirstMessage!;
        newSpawnedCharacter.CharacterImageLink = fullCommonCharacter.CharacterImageLink;
        newSpawnedCharacter.CharacterAuthor = fullCommonCharacter.CharacterAuthor ?? "unknown";
        newSpawnedCharacter.IsNfsw = fullCommonCharacter.IsNfsw;
        newSpawnedCharacter.DiscordChannelId = (ulong)webhook.ChannelId!;
        newSpawnedCharacter.WebhookId = webhook.Id;
        newSpawnedCharacter.WebhookToken = webhook.Token;
        newSpawnedCharacter.CallPrefix = callPrefix.ToLower();
        newSpawnedCharacter.ResponseDelay = 3;
        newSpawnedCharacter.FreewillFactor = 3;
        newSpawnedCharacter.EnableSwipes = false;
        newSpawnedCharacter.FreewillContextSize = 3000;
        newSpawnedCharacter.EnableQuotes = false;
        newSpawnedCharacter.EnableStopButton = true;
        newSpawnedCharacter.SkipNextBotMessage = false;
        newSpawnedCharacter.LastCallerDiscordUserId = 0;
        newSpawnedCharacter.LastDiscordMessageId = 0;
        newSpawnedCharacter.MessagesSent = 0;
        newSpawnedCharacter.LastCallTime = DateTime.Now;

        await db.SaveChangesAsync();

        return newSpawnedCharacter;
    }


    public static async Task<IWebhook> CreateDiscordWebhookAsync(IIntegrationChannel channel, string name, string? imageUrl)
    {
        var characterName = name.Trim();
        var match = DISCORD_REGEX.Match(characterName);
        if (match.Success)
        {
            var discordCensored = match.Value.Replace('o', 'о').Replace('O', 'О');
            characterName = characterName.Replace(match.Value, discordCensored);
        }

        var avatar = await CommonHelper.DownloadFileAsync(imageUrl);

        using var avatarInput = new MemoryStream();
        using var avatarOutput = new MemoryStream();

        if (avatar is null)
        {
            var defaultAvatar = await File.ReadAllBytesAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "img", BotConfig.DEFAULT_AVATAR_FILE));
            await avatarOutput.WriteAsync(defaultAvatar);
        }
        else
        {
            await avatar.CopyToAsync(avatarInput);
            avatarInput.Seek(0, SeekOrigin.Begin);

            if (avatarInput.Length < 10240000)
            {
                await avatarInput.CopyToAsync(avatarOutput);
            }
            else
            {
                var settings = new ProcessImageSettings
                {
                    Interpolation = InterpolationSettings.Cubic,
                    ResizeMode = CropScaleMode.Crop,
                    Anchor = CropAnchor.Top,
                    HybridMode = HybridScaleMode.Turbo,
                    OrientationMode = OrientationMode.Normalize,
                    ColorProfileMode = ColorProfileMode.ConvertToSrgb,
                    EncoderOptions = new JpegEncoderOptions(0, ChromaSubsampleMode.Default),
                    Width = 600,
                };

                MagicImageProcessor.ProcessImage(avatarInput, avatarOutput, settings);
            }
        }

        avatarOutput.Seek(0, SeekOrigin.Begin);

        return channel is SocketThreadChannel { ParentChannel: ITextChannel parentChannel }
                ? await parentChannel.CreateWebhookAsync(characterName, avatarOutput)
                : await channel.CreateWebhookAsync(characterName, avatarOutput);
    }


    public static async Task<ulong> SendGreetingAsync(this ISpawnedCharacter spawnedCharacter, string username, ulong? threadId = null)
    {
        if (string.IsNullOrWhiteSpace(spawnedCharacter.CharacterFirstMessage))
        {
            return 0;
        }

        var characterMessage = spawnedCharacter.CharacterFirstMessage
                                               .Replace("{{char}}", spawnedCharacter.CharacterName)
                                               .Replace("{{user}}", $"**{username}**");

        return await SendMessageAsync(spawnedCharacter, characterMessage, threadId);
    }


    public static async Task<ulong> SendMessageAsync(this ISpawnedCharacter spawnedCharacter, string characterMessage, ulong? threadId = null)
    {
        try
        {
            var webhookClient = MemoryStorage.CachedWebhookClients.FindOrCreate(spawnedCharacter);

            if (characterMessage.Length <= 2000)
            {
                return await (threadId is null ? webhookClient.SendMessageAsync(characterMessage) : webhookClient.SendMessageAsync(characterMessage, threadId: threadId));
            }

            var chunkSize = characterMessage.Length > 3990 ? 1990 : characterMessage.Length / 2;
            var chunks = characterMessage.Chunk(chunkSize).Select(c => new string(c)).ToArray();

            ulong messageId;
            if (threadId is null)
            {
                messageId = await webhookClient.SendMessageAsync(chunks[0]);
                var channel = (ITextChannel)CharacterEngineBot.DiscordClient.GetChannel(threadId ?? spawnedCharacter.DiscordChannelId);
                var message = await channel.GetMessageAsync(messageId);
                var thread = await channel.CreateThreadAsync("[MESSAGE LENGTH LIMIT EXCEEDED]", message: message);

                for (var i = 1; i < chunks.Length; i++)
                {
                    await webhookClient.SendMessageAsync(chunks[i], threadId: thread.Id);
                }

                await thread.ModifyAsync(t => { t.Archived = true; });
            }
            else
            {
                messageId = await webhookClient.SendMessageAsync(chunks[0] + "[...]", threadId: threadId);
            }

            return messageId;
        }
        catch (Exception e)
        {
            var webhookExceptionCheck = e.ValidateWebhookException();
            if (webhookExceptionCheck.valid)
            {
                MemoryStorage.CachedWebhookClients.Remove(spawnedCharacter.WebhookId);
                MemoryStorage.CachedCharacters.Remove(spawnedCharacter.Id);

                await DatabaseHelper.DeleteSpawnedCharacterAsync(spawnedCharacter);
            }

            throw;
        }
    }

    #endregion


    #region Messages format

    private const string INHERITED_FROM_CHANNEL = " (inherited from channel-wide setting)";
    private const string INHERITED_FROM_GUILD = " (inherited from server-wide setting)";
    private const string INHERITED_FROM_DEFAULT = " (default)";

    public static async Task<string> GetCharacterMessagesFormatAsync(ISpawnedCharacter spawnedCharacter)
    {
        await using var db = DatabaseHelper.GetDbContext();

        var channelWithGuild = db.DiscordChannels.Include(c => c.DiscordGuild).Where(c => c.Id == spawnedCharacter.DiscordChannelId);
        var getChannelFormatAsync = channelWithGuild.Select(c => c.MessagesFormat);
        var getGuildFormatAsync = channelWithGuild.Select(c => c.DiscordGuild.MessagesFormat);

        string format;
        string? inheritNote = null;
        if (spawnedCharacter.MessagesFormat is string characterMessagesFormat)
        {
            format = characterMessagesFormat;
        }
        else if (await getChannelFormatAsync.FirstOrDefaultAsync() is string channelFormat)
        {
            format = channelFormat;
            inheritNote = INHERITED_FROM_CHANNEL;
        }
        else if (await getGuildFormatAsync.FirstOrDefaultAsync() is string guildFormat)
        {
            format = guildFormat;
            inheritNote = INHERITED_FROM_GUILD;
        }
        else
        {
            format = GetDefaultMessagesFormat();
            inheritNote = INHERITED_FROM_DEFAULT;
        }

        return SuccessFormatShowMessage($"{spawnedCharacter.GetMention()}'s", format, inheritNote);
    }


    public static async Task<string> GetChannelMessagesFormatAsync(ulong channelId, ulong guildId)
    {
        await using var db = DatabaseHelper.GetDbContext();

        var getChannelFormatAsync = db.DiscordChannels.Where(c => c.Id == channelId).Select(c => c.MessagesFormat);
        var getGuildFormatAsync = db.DiscordGuilds.Where(g => g.Id == guildId).Select(g => g.MessagesFormat);

        string format;
        string? inheritNote = null;
        if (await getChannelFormatAsync.FirstAsync() is string channelFormat)
        {
            format = channelFormat;
        }
        else if (await getGuildFormatAsync.FirstAsync() is string guildFormat)
        {
            format = guildFormat;
            inheritNote = INHERITED_FROM_GUILD;
        }
        else
        {
            format = GetDefaultMessagesFormat();
            inheritNote = INHERITED_FROM_DEFAULT;
        }

        return SuccessFormatShowMessage("Current channel", format, inheritNote);
    }


    public static async Task<string> GetGuildMessagesFormatAsync(ulong guildId)
    {
        await using var db = DatabaseHelper.GetDbContext();

        var guildFormat = await db.DiscordGuilds.Where(g => g.Id == guildId).Select(g => g.MessagesFormat).FirstAsync();

        string? inheritNote = null;
        if (guildFormat is null)
        {
            guildFormat = GetDefaultMessagesFormat();
            inheritNote = INHERITED_FROM_DEFAULT;
        }

        return SuccessFormatShowMessage("Current server", guildFormat, inheritNote);
    }


    public static async Task<string> UpdateCharacterMessagesFormatAsync(ISpawnedCharacter spawnedCharacter, string? newFormat)
    {
        ValidateMessagesFormat(newFormat);

        spawnedCharacter.MessagesFormat = newFormat;
        await DatabaseHelper.UpdateSpawnedCharacterAsync(spawnedCharacter);

        return SuccessFormatUpdateMessage($"Messages format for character {spawnedCharacter.GetMention()}", newFormat);
    }


    public static async Task<string> UpdateChannelMessagesFormatAsync(ulong channelId, string? newFormat)
    {
        ValidateMessagesFormat(newFormat);

        await using var db = DatabaseHelper.GetDbContext();
        var channel = await db.DiscordChannels.FirstAsync(c => c.Id == channelId);
        channel.MessagesFormat = newFormat;
        await db.SaveChangesAsync();

        return SuccessFormatUpdateMessage("Default channel-wide messages format", newFormat);
    }


    public static async Task<string> UpdateGuildMessagesFormatAsync(ulong guildId, string? newFormat)
    {
        ValidateMessagesFormat(newFormat);

        await using var db = DatabaseHelper.GetDbContext();
        var guild = await db.DiscordGuilds.FirstAsync(g => g.Id == guildId);
        guild.MessagesFormat = newFormat;
        await db.SaveChangesAsync();

        return SuccessFormatUpdateMessage("Default server-wide messages format", newFormat);
    }



    private static void ValidateMessagesFormat(string? newFormat)
    {
        if (newFormat is null)
        {
            return;
        }

        if (!newFormat.Contains(MH.MF_MSG))
        {
            throw new UserFriendlyException($"Add {MH.MF_MSG} placeholder");
        }

        if (newFormat.Contains(MH.MF_REF_MSG))
        {
            var iBegin = newFormat.IndexOf(MH.MF_REF_BEGIN, StringComparison.Ordinal);
            var iEnd = newFormat.IndexOf(MH.MF_REF_END, StringComparison.Ordinal);
            var iMsg = newFormat.IndexOf(MH.MF_REF_MSG, StringComparison.Ordinal);

            if (iBegin == -1 || iEnd == -1 || iBegin > iMsg || iEnd < iMsg)
            {
                throw new UserFriendlyException($"{MH.MF_REF_MSG} placeholder can work only with {MH.MF_REF_BEGIN} and {MH.MF_REF_END} placeholders around it: `{MH.MF_REF_BEGIN} {MH.MF_REF_MSG} {MH.MF_REF_END}`");
            }
        }
    }


    private static string SuccessFormatUpdateMessage(string target, string? format)
        => new StringBuilder(MT.OK_SIGN_DISCORD).Append(' ')
                                                .Append(target)
                                                .Append(format is null ? " reset to default value" : " was changed")
                                                .Append(" successfully.\n\n**Preview:**\n")
                                                .Append(MH.BuildMessageFormatPreview(format ?? GetDefaultMessagesFormat()))
                                                .ToString();


    private static string SuccessFormatShowMessage(string target, string format, string? inheritNote)
    {
        var message = new StringBuilder(target).Append(" messages format");

        if (inheritNote is not null)
        {
            message.Append(inheritNote);
        }

        message.Append(":\n```")
               .Append(format)
               .Append("```\n**Preview:**\n")
               .Append(MH.BuildMessageFormatPreview(format));

        return message.ToString();
    }


    private static string GetDefaultMessagesFormat()
        => BotConfig.DEFAULT_MESSAGES_FORMAT.Replace("\\n", "\\n\n");

    #endregion


    #region Validations

    public static void ValidateUser(IGuildUser user, ITextChannel channel)
    {
        var validation = WatchDog.ValidateUser(user, channel);

        switch (validation.Result)
        {
            case WatchDogValidationResult.Passed:
            {
                return;
            }
            case WatchDogValidationResult.Warning:
            {
                const string message = $"{MT.WARN_SIGN_DISCORD} You are interacting with the bot too frequently, please slow down or you may result being temporarily blocked";
                _ = channel.SendMessageAsync(user.Mention, embed: message.ToInlineEmbed(Color.Orange));
                break;
            }
            case WatchDogValidationResult.Blocked:
            {
                if (validation.BlockedUntil.HasValue)
                {
                    var time = validation.BlockedUntil.Value - DateTime.Now;
                    var message = $"Your were blocked from interacting with the bot for {(time.TotalHours >= 1 ? $"{time.TotalHours} hour(s)" : $"{time.TotalMinutes} minute(s)")}";
                    _ = channel.SendMessageAsync(user.Mention, embed: message.ToInlineEmbed(Color.Red));
                }

                throw new UnauthorizedAccessException();
            }
        }
    }


    public static async Task ValidateAccessLevelAsync(AccessLevels requiredAccessLevel, SocketGuildUser user)
    {
        if (BotConfig.OWNER_USERS_IDS.Contains(user.Id))
        {
            return;
        }

        var userIsGuildAdmin = user.Id == user.Guild.OwnerId || user.Roles.Any(role => role.Permissions.Administrator);

        switch (requiredAccessLevel)
        {
            case AccessLevels.BotAdmin:
            {
                throw new UnauthorizedAccessException();
            }
            case AccessLevels.GuildAdmin:
            {
                if (userIsGuildAdmin)
                {
                    return;
                }

                throw new UserFriendlyException("Only server administrators are allowed to access this command.");
            }
            case AccessLevels.Manager:
            {
                if (userIsGuildAdmin || await UserIsManagerAsync(user))
                {
                    return;
                }

                throw new UserFriendlyException("Only managers are allowed to access this command. Managers list can be seen with `/managers` command.");
            }
            default:
            {
                throw new UnauthorizedAccessException();
            }
        }
    }


    private static async Task<bool> UserIsManagerAsync(SocketGuildUser user)
    {
        await using var db = DatabaseHelper.GetDbContext();

        var managersIds = await db.GuildBotManagers
                                  .Where(manager => manager.DiscordGuildId == user.Guild.Id)
                                  .Select(manager => manager.DiscordUserOrRoleId)
                                  .ToArrayAsync();

        if (managersIds.Length == 0)
        {
            return false;
        }

        var userRolesIds = user.Roles.Select(r => r.Id).ToArray();
        return managersIds.Any(id => id == user.Id || userRolesIds.Contains(id));
    }


    private const string DISABLE_WARN_PROMPT = "If these restrictions were imposed intentionally, then you can disable this warning with `/channel no-warn` or `/server no-warn` command.";

    private static readonly ChannelPermission[] REQUIRED_PERMS = [
        ChannelPermission.ViewChannel, ChannelPermission.SendMessages, ChannelPermission.AddReactions, ChannelPermission.EmbedLinks, ChannelPermission.AttachFiles, ChannelPermission.ManageWebhooks,
        ChannelPermission.CreatePublicThreads, ChannelPermission.CreatePrivateThreads, ChannelPermission.SendMessagesInThreads, ChannelPermission.ManageThreads, ChannelPermission.UseExternalEmojis
    ];

    public static async Task ValidateChannelPermissionsAsync(IChannel channel)
    {
        var textChannel = channel switch
        {
            SocketThreadChannel { ParentChannel: ITextChannel threadParentChannel } => threadParentChannel,
            ITextChannel cTextChannel => cTextChannel,
            _ => throw new UserFriendlyException("Bot can operate only in text channels")
        };

        var guild = (SocketGuild)textChannel.Guild;
        var botRoles = guild.CurrentUser.Roles;

        if (botRoles.Select(br => br.Permissions).Any(perm => perm.Administrator))
        {
            return;
        }

        if (await channel.GetUserAsync(CharacterEngineBot.DiscordClient.CurrentUser.Id) is null)
        {
            throw new UserFriendlyException("Bot has no permission to view this channel");
        }

        if (MemoryStorage.CachedChannels.TryGetValue(channel.Id, out var noWarn) && noWarn)
        {
            return;
        }

        var everyoneRoleId = textChannel.Guild.EveryoneRole.Id;
        var botChannelPermOws = textChannel.PermissionOverwrites.Where(BotAffectedByOw).ToList();

        var botAllowedPerms = botRoles.SelectMany(ChannelPerms).Concat(botChannelPermOws.SelectMany(AllowedPerms)).ToList();
        var channelDeniedPermOws = botChannelPermOws.Where(ow => ow.TargetId != everyoneRoleId).SelectMany(DeniedPerms).ToList();

        var missingPerms = new List<ChannelPermission>();
        var prohibitiveOws = new List<(ChannelPermission perm, string target)>();

        foreach (var requiredPerm in REQUIRED_PERMS)
        {
            if (botAllowedPerms.Contains(requiredPerm))
            {
                var allowedAndProhibitedPerm = channelDeniedPermOws.Where(ow => ow.perm == requiredPerm);
                prohibitiveOws.AddRange(allowedAndProhibitedPerm);
            }
            else
            {
                missingPerms.Add(requiredPerm);
            }
        }

        if (missingPerms.Count != 0)
        {
            var msg = $"**{MT.WARN_SIGN_DISCORD} There are permissions required for the bot to operate in this channel that are missing:**\n" +
                      $"```{string.Join("\n", missingPerms.Select(perm => $"> {perm:G}"))}```\n{DISABLE_WARN_PROMPT}";

            throw new UserFriendlyException(msg, bold: false);
        }

        if (prohibitiveOws.Count != 0)
        {
            var msg = $"**{MT.WARN_SIGN_DISCORD} This channel has some prohibitive permission overwrites applied to the bot, which may affect its work:**\n" +
                      $"```{string.Join("\n", prohibitiveOws.Select(ow => $"> {ow.perm:G} | Prohibited for {ow.target}"))}```\n{DISABLE_WARN_PROMPT}";

            throw new UserFriendlyException(msg, bold: false);
        }

        return;

        #region Shortcuts

        IEnumerable<ChannelPermission> ChannelPerms(SocketRole role)
            => role.Permissions.ToList().Cast<ChannelPermission>();

        IEnumerable<ChannelPermission> AllowedPerms(Overwrite ow)
            => ow.Permissions.ToAllowList();

        IEnumerable<(ChannelPermission perm, string target)> DeniedPerms(Overwrite ow)
            => ow.Permissions.ToDenyList().Select(perm => (perm, target: GetFullTargetAsync(ow)));

        string GetFullTargetAsync(Overwrite ow)
        {
            try
            {
                if (ow.TargetType is PermissionTarget.Role)
                {
                    var role = textChannel.Guild.Roles.First(g => g.Id == ow.TargetId);
                    return $"role @{role.Name}";
                }

                var user = textChannel.GetUserAsync(ow.TargetId).GetAwaiter().GetResult();
                return $"user @{user.DisplayName}";
            }
            catch
            {
                return $"{ow.TargetType:G} {ow.TargetId}".ToLower();
            }
        }

        bool BotAffectedByOw(Overwrite ow)
            => ow.TargetId == guild.CurrentUser.Id || guild.CurrentUser.Roles.Any(role => role.Id == ow.TargetId);

        #endregion
    }

    #endregion
}
