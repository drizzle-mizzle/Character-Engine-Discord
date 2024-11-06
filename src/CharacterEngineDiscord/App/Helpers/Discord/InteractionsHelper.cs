using System.Text.RegularExpressions;
using CharacterAi.Client.Exceptions;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.IntegrationModules;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NLog;
using PhotoSauce.MagicScaler;
using SakuraAi.Client.Exceptions;
using SakuraAi.Client.Models.Common;
using DI = CharacterEngine.App.Helpers.Infrastructure.DependencyInjectionHelper;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;

namespace CharacterEngine.App.Helpers.Discord;


public static class InteractionsHelper
{
    public const string COMMAND_SEPARATOR = "~sep~";

    private static ILogger _log = DI.GetLogger;
    private static readonly Regex DISCORD_REGEX = new("discord", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);


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
        var channel = await DI.GetDiscordSocketClient.GetChannelAsync(channelId) as ITextChannel;

        if (channel is null)
        {
            throw new Exception($"Failed to get channel {channelId}");
        }

        var webhook = await CreateDiscordWebhookAsync(channel, commonCharacter);
        var webhookClient = new DiscordWebhookClient(webhook.Id, webhook.Token);
        MemoryStorage.CachedWebhookClients.Add(webhook.Id, webhookClient);

        var newSpawnedCharacter = await DatabaseHelper.CreateSpawnedCharacterAsync(commonCharacter, webhook);
        MemoryStorage.CachedCharacters.Add(newSpawnedCharacter);

        return newSpawnedCharacter;
    }


    public static async Task<IWebhook> CreateDiscordWebhookAsync(IIntegrationChannel channel, ICharacter character)
    {
        var characterName = character.CharacterName.Trim();
        var match = DISCORD_REGEX.Match(characterName);
        if (match.Success)
        {
            var discordCensored = match.Value.Replace('o', 'о').Replace('O', 'О');
            characterName = characterName.Replace(match.Value, discordCensored);
        }

        var avatar = await CommonHelper.DownloadFileAsync(character.CharacterImageLink);

        using var avatarInput = new MemoryStream();
        using var avatarOutput = new MemoryStream();


        if (avatar is null)
        {
            var defaultAvatar = await File.ReadAllBytesAsync(Path.Combine("./Settings", "img", BotConfig.DEFAULT_AVATAR_FILE));
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

        if (avatarOutput.Length >= 10240000)
        {
            var defaultAvatar = await File.ReadAllBytesAsync(Path.Combine("./Settings", "img", BotConfig.DEFAULT_AVATAR_FILE));

            avatarOutput.SetLength(0);
            await avatarOutput.WriteAsync(defaultAvatar);
        }

        avatarOutput.Seek(0, SeekOrigin.Begin);
        return await channel.CreateWebhookAsync(characterName, avatarOutput);;
    }


    public static Task SendGreetingAsync(this ISpawnedCharacter spawnedCharacter, string username)
    {
        var characterMessage = spawnedCharacter.CharacterFirstMessage
                                               .Replace("{{char}}", spawnedCharacter.CharacterName)
                                               .Replace("{{user}}", $"**{username}**");

        return SendMessageAsync(spawnedCharacter, characterMessage);
    }


    public static async Task<ulong> SendMessageAsync(this ISpawnedCharacter spawnedCharacter, string characterMessage)
    {
        var webhookClient = MemoryStorage.CachedWebhookClients.FindOrCreate(spawnedCharacter);

        if (characterMessage.Length <= 2000)
        {
            return await webhookClient.SendMessageAsync(characterMessage);
        }

        var chunkSize = characterMessage.Length > 3990 ? 1990 : characterMessage.Length / 2;
        var chunks = characterMessage.Chunk(chunkSize).Select(c => new string(c)).ToArray();

        var messageId = await webhookClient.SendMessageAsync(chunks[0]);

        var channel = (ITextChannel)await DI.GetDiscordSocketClient.GetChannelAsync(spawnedCharacter.DiscordChannelId);
        var message = await channel.GetMessageAsync(messageId);
        var thread = await channel.CreateThreadAsync("[MESSAGE LENGTH LIMIT EXCEEDED]", message: message);

        for (var i = 1; i < chunks.Length; i++)
        {
            await webhookClient.SendMessageAsync(chunks[i], threadId: thread.Id);
        }

        // await thread.ModifyAsync(t => { t.Archived = t.Locked = true; });
        await thread.ModifyAsync(t => { t.Archived = true; });

        return messageId;
    }

    #endregion


    #region CreateIntegration

    public static async Task SendSakuraAiMailAsync(SocketModal modal)
    {
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();
        var sakuraAiModule = (SakuraAiModule)IntegrationType.SakuraAI.GetIntegrationModule();

        SakuraSignInAttempt attempt;
        try
        {
            attempt = await sakuraAiModule.SendLoginEmailAsync(email);
        }
        catch (SakuraException e)
        {
            await modal.FollowupAsync(embed: $"{MessagesTemplates.WARN_SIGN_DISCORD} SakuraAI responded with error:\n```{e.Message}```".ToInlineEmbed(Color.Red));
            return;
        }

        // Respond to user
        var msg = $"{IntegrationType.SakuraAI.GetIcon()} **SakuraAI**\n\n" +
                  $"Confirmation mail was sent to **{email}**. Please check your mailbox and follow further instructions.\n\n" +
                  $"- *It's recommended to log out of your SakuraAI account in the browser first, before you open a link in the mail; or simply open it in [incognito tab](https://support.google.com/chrome/answer/95464?hl=en&co=GENIE.Platform%3DDesktop&oco=1#:~:text=New%20Incognito%20Window).*\n" +
                  $"- *It may take up to a minute for the bot to react on succeful confirmation.*";

        await modal.FollowupAsync(embed: msg.ToInlineEmbed(bold: false, color: Color.Green));

        // Update db
        var data = StoredActionsHelper.CreateSakuraAiEnsureLoginData(attempt, (ulong)modal.ChannelId!, modal.User.Id);
        var newAction = new StoredAction(StoredActionType.SakuraAiEnsureLogin, data, maxAttemtps: 25);

        await using var db = DatabaseHelper.GetDbContext();
        await db.StoredActions.AddAsync(newAction);
        await db.SaveChangesAsync();
    }


    public static async Task SendCharacterAiMailAsync(SocketModal modal)
    {
        var email = modal.Data.Components.First(c => c.CustomId == "email").Value.Trim();
        var caiModule = (CaiModule)IntegrationType.CharacterAI.GetIntegrationModule();

        try
        {
            await caiModule.SendLoginEmailAsync(email);
        }
        catch (CharacterAiException e)
        {
            await modal.FollowupAsync(embed: $"{MessagesTemplates.WARN_SIGN_DISCORD} CharacterAI responded with error:\n```{e.Message}```".ToInlineEmbed(Color.Red));
            return;
        }

        var msg = $"{IntegrationType.CharacterAI.GetIcon()} **CharacterAI**\n\n" +
                  $"Sign in mail was sent to **{email}**, please check your mailbox. You should've received a sign in link for CharacterAI in it - **DON'T OPEN IT**, copy it and then paste in `/integration confirm` command.\n" +
                  $"**Example:**\n*`/integration confirm type:CharacterAI data:https://character.ai/login/xxx`*";

        await modal.FollowupAsync(embed: msg.ToInlineEmbed(bold: false, color: Color.Green));
    }

    #endregion


    #region SharedSlashCommands

    private const string INHERITED_FROM_DEFAULT = " (default)";
    private const string INHERITED_FROM_GUILD = " (inherited from server-wide setting)";
    private const string INHERITED_FROM_CHANNEL = " (inherited from channel-wide setting)";

    public static async Task<string> SharedMessagesFormatAsync(MessagesFormatTarget target, MessagesFormatAction action, object idOrCharacterObject, string? newFormat = null)
    {
        await using var db = DatabaseHelper.GetDbContext();
        switch (action)
        {
            case MessagesFormatAction.show:
            {
                string? format = default;
                string? inheritNote = default;
                string msgBegin = default!;

                switch (target)
                {
                    case MessagesFormatTarget.guild:
                    {
                        msgBegin = "Current server";

                        format = await GetGuildFormatAsync((ulong)idOrCharacterObject);

                        break;
                    }
                    case MessagesFormatTarget.channel:
                    {
                        msgBegin = "Current channel";

                        format = await GetChannelFormatAsync((ulong)idOrCharacterObject);
                        if (format is not null)
                        {
                            break;
                        }

                        var guildId = await db.DiscordChannels.Where(c => c.Id == (ulong)idOrCharacterObject).Select(c => c.DiscordGuildId).FirstAsync();

                        format = await GetGuildFormatAsync(guildId);
                        if (format is not null)
                        {
                            inheritNote = INHERITED_FROM_GUILD;
                        }

                        break;
                    }
                    case MessagesFormatTarget.character:
                    {
                        var character = (ISpawnedCharacter)idOrCharacterObject;
                        msgBegin = $"**{character!.CharacterName}**'s";

                        format = character.MessagesFormat;
                        if (format is not null)
                        {
                            break;
                        }

                        format = await GetChannelFormatAsync(character.DiscordChannelId);
                        if (format is not null)
                        {
                            inheritNote = INHERITED_FROM_CHANNEL;
                            break;
                        }

                        var guildId = await db.DiscordChannels.Where(c => c.Id == character.DiscordChannelId).Select(c => c.DiscordGuildId).FirstAsync();
                        format = await GetGuildFormatAsync(guildId);
                        if (format is not null)
                        {
                            inheritNote = INHERITED_FROM_GUILD;
                        }

                        break;
                    }
                }

                if (format is null)
                {
                    format = GetDefaultFormat();
                    inheritNote = INHERITED_FROM_DEFAULT;
                }

                var preview = MH.BuildMessageFormatPreview(format);

                return $"{msgBegin} messages format{inheritNote}:\n" +
                       $"```{format}```\n" +
                       $"**Preview:**\n{preview}";

                Task<string?> GetChannelFormatAsync(ulong channelId)
                    => db.DiscordChannels.Where(c => c.Id == channelId).Select(c => c.MessagesFormat).FirstAsync();

                Task<string?> GetGuildFormatAsync(ulong guildId)
                    => db.DiscordGuilds.Where(g => g.Id == guildId).Select(g => g.MessagesFormat).FirstAsync();
            }
            case MessagesFormatAction.update:
            {
                if (newFormat is null)
                {
                    throw new UserFriendlyException("Specify a new messages format");
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

                var msgBegin = await UpdateFormatAsync(newFormat);

                return $"{MessagesTemplates.OK_SIGN_DISCORD} {msgBegin} was changed successfully.\n" +
                       $"**Preview:**\n" +
                       $"{MH.BuildMessageFormatPreview(newFormat)}";
            }
            case MessagesFormatAction.resetDefault:
            {
                var format = GetDefaultFormat();
                var msgBegin = await UpdateFormatAsync(format);

                return $"{MessagesTemplates.OK_SIGN_DISCORD} {msgBegin} was reset to default value successfully.\n" +
                       $"**Preview:**\n" +
                       $"{MH.BuildMessageFormatPreview(format)}";
            }
        }

        return default!;

        async Task<string> UpdateFormatAsync(string format)
        {
            switch (target)
            {
                case MessagesFormatTarget.guild:
                {
                    var guild = await db.DiscordGuilds.FirstAsync(g => g.Id == (ulong)idOrCharacterObject);
                    guild.MessagesFormat = format;
                    await db.SaveChangesAsync();

                    return "Default server-wide messages format";
                }
                case MessagesFormatTarget.channel:
                {
                    var channel = await db.DiscordChannels.FirstAsync(c => c.Id == (ulong)idOrCharacterObject);
                    channel.MessagesFormat = format;
                    await db.SaveChangesAsync();

                    return "Default channel-wide messages format";
                }
                case MessagesFormatTarget.character:
                {
                    var character = (await DatabaseHelper.GetSpawnedCharacterByIdAsync((Guid)idOrCharacterObject))!;
                    character.MessagesFormat = format;
                    await DatabaseHelper.UpdateSpawnedCharacterAsync(character);

                    return $"Messages format for character {character.CharacterName}";
                }
            }

            return default!;
        }

        string GetDefaultFormat()
            => BotConfig.DEFAULT_MESSAGES_FORMAT.Replace("\\n", "\\n\n");
    }

    #endregion


    #region Validations

    public static async Task ValidateUserAsync(SocketInteraction interaction)
    {
        var validationResult = WatchDog.ValidateUser((IGuildUser)interaction.User);

        switch (validationResult)
        {
            case WatchDogValidationResult.Blocked:
            {
                await interaction.FollowupAsync();
                throw new UnauthorizedAccessException();
            }
            case WatchDogValidationResult.Warning:
            {
                _ = interaction.Channel.SendMessageAsync(interaction.User.Mention, embed: MessagesTemplates.RATE_LIMIT_WARNING);
            }

            return;
        }
    }


    public static async Task ValidateAccessLevelAsync(AccessLevels requiredAccessLevel, SocketGuildUser user)
    {
        if (BotConfig.OWNER_USERS_IDS.Contains(user.Id))
        {
            return;
        }

        var userIsGuildOwner = user.Id == user.Guild.OwnerId || user.Roles.Any(role => role.Permissions.Administrator);

        switch (requiredAccessLevel)
        {
            case AccessLevels.BotAdmin:
            {
                throw new UnauthorizedAccessException();
            }
            case AccessLevels.GuildAdmin:
            {
                if (userIsGuildOwner)
                {
                    return;
                }

                throw new UserFriendlyException("Only server administrators are allowed to access this command.");
            }
            case AccessLevels.Manager:
            {
                if (userIsGuildOwner || await UserIsManagerAsync(user))
                {
                    return;
                }

                throw new UserFriendlyException("Only managers are allowed to access this command. Managers can be added by server administrators with `/managers` command.");
            }
            default:
            {
                throw new UnauthorizedAccessException();
            }
        }
    }


    private static Task<bool> UserIsManagerAsync(SocketGuildUser user)
    {
        using var db = DatabaseHelper.GetDbContext();
        return db.GuildBotManagers.AnyAsync(manager => manager.DiscordGuildId == user.Guild.Id
                                                    && manager.UserId == user.Id);
    }


    private const string DISABLE_WARN_PROMPT = "If these restrictions were imposed intentionally, then you can disable this warning with `/channel no-warn` or `/server no-warn` command.";

    private static readonly ChannelPermission[] REQUIRED_PERMS = [
        ChannelPermission.ViewChannel, ChannelPermission.SendMessages, ChannelPermission.AddReactions, ChannelPermission.EmbedLinks, ChannelPermission.AttachFiles, ChannelPermission.ManageWebhooks,
        ChannelPermission.CreatePublicThreads, ChannelPermission.CreatePrivateThreads, ChannelPermission.SendMessagesInThreads, ChannelPermission.ManageThreads, ChannelPermission.UseExternalEmojis
    ];

    public static async Task ValidateChannelPermissionsAsync(IChannel channel)
    {
        if (channel is not ITextChannel textChannel)
        {
            throw new UserFriendlyException("Bot can operatein only in text channels");
        }

        var guild = (SocketGuild)textChannel.Guild;

        var botGuildUser = guild.CurrentUser;
        if (botGuildUser is null)
        {
            throw new UserFriendlyException("Bot has no permission to view this channel");
        }

        var botRoles = botGuildUser.Roles;
        if (botRoles.Select(br => br.Permissions).Any(perm => perm.Administrator))
        {
            return;
        }

        await using var db = DatabaseHelper.GetDbContext();
        var noWarn = await db.DiscordChannels.Where(c => c.Id == textChannel.Id).Select(c => c.NoWarn).FirstAsync();
        if (noWarn)
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
            var msg = $"**{MessagesTemplates.WARN_SIGN_DISCORD} There are permissions required for the bot to operate in this channel that are missing:**\n" +
                      $"```{string.Join("\n", missingPerms.Select(perm => $"> {perm:G}"))}```\n{DISABLE_WARN_PROMPT}";

            throw new UserFriendlyException(msg, bold: false);
        }

        if (prohibitiveOws.Count != 0)
        {
            var msg = $"**{MessagesTemplates.WARN_SIGN_DISCORD} This channel has some prohibitive permission overwrites applied to the bot, which may affect its work:**\n" +
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
            => ow.TargetId == botGuildUser.Id || botGuildUser.Roles.Any(role => role.Id == ow.TargetId);

        #endregion
    }

    #endregion
}
