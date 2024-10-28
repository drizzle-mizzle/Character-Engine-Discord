using System.Text.RegularExpressions;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Helpers.Common;
using CharacterEngineDiscord.Helpers.Integrations;
using CharacterEngineDiscord.IntegrationModules;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Common;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NLog;
using SakuraAi.Client.Exceptions;
using SakuraAi.Client.Models.Common;
using DI = CharacterEngine.App.Helpers.Infrastructure.DependencyInjectionHelper;

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

    public static Embed BuildSearchResultList(SearchQuery searchQuery)
    {
        var type = searchQuery.IntegrationType;
        var embed = new EmbedBuilder().WithColor(type.GetColor());

        var title = $"{type.GetIcon()} {type:G}";
        var listTitle = $"({searchQuery.Characters.Count}) Characters found by query **\"{searchQuery.OriginalQuery}\"**:";
        embed.AddField(title, listTitle);

        var rows = Math.Min(searchQuery.Characters.Count, 10);
        var pageMultiplier = (searchQuery.CurrentPage - 1) * 10;

        for (var row = 1; row <= rows; row++)
        {
            var characterNumber = row + pageMultiplier;
            var character = searchQuery.Characters.ElementAt(characterNumber - 1);

            var rowTitle = $"{characterNumber}. {character.CharacterName}";
            var rowContent = $"{type.GetStatLabel()}: {character.CharacterStat} **|** Author: [__{character.CharacterAuthor}__]({character.GetAuthorLink()}) **|** [[__character link__]({character.GetCharacterLink()})]";
            if (searchQuery.CurrentRow == row)
            {
                rowTitle += " - ✅";
            }

            embed.AddField(rowTitle, rowContent);
        }

        embed.WithFooter($"Page {searchQuery.CurrentPage}/{searchQuery.Pages}");

        return embed.Build();
    }


    public static async Task<ISpawnedCharacter> SpawnCharacterAsync(ulong channelId, CommonCharacter commonCharacter)
    {
        var discordClient = DI.GetDiscordSocketClient;
        if (await discordClient.GetChannelAsync(channelId) is not ITextChannel channel)
        {
            throw new Exception($"Failed to get channel {channelId}");
        }

        var webhook = await discordClient.CreateDiscordWebhookAsync(channel, commonCharacter);
        var newSpawnedCharacter = await DatabaseHelper.CreateSpawnedCharacterAsync(commonCharacter, webhook);

        return newSpawnedCharacter;
    }


    public static async Task<IWebhook> CreateDiscordWebhookAsync(this DiscordSocketClient discordClient, IIntegrationChannel channel, ICharacter character)
    {
        var characterName = character.CharacterName.Trim();
        var match = DISCORD_REGEX.Match(characterName);
        if (match.Success)
        {
            var discordCensored = match.Value.Replace('o', 'о').Replace('O', 'О');
            characterName = characterName.Replace(match.Value, discordCensored);
        }

        Stream? avatar = null;
        if (character.CharacterImageLink is not null)
        {
            try
            {
                avatar = await MemoryStorage.CommonHttpClient.GetStreamAsync(character.CharacterImageLink);
            }
            catch (Exception e)
            {
                await discordClient.ReportErrorAsync(e, CommonHelper.NewTraceId());
            }
        }

        // TODO: thread safety
        // avatar ??= File.OpenRead(BotConfig.DEFAULT_AVATAR_FILE);

        var webhook = await channel.CreateWebhookAsync(characterName, avatar);
        return webhook;
    }


    public static Task SendGreetingAsync(this ICharacter character, string username)
    {
        var characterMessage = character.CharacterFirstMessage
                                        .Replace("{{char}}", character.CharacterName)
                                        .Replace("{{user}}", $"**{username}**");

        return SendMessageAsync((ISpawnedCharacter)character, characterMessage);
    }


    public static async Task SendMessageAsync(this ISpawnedCharacter spawnedCharacter, string characterMessage)
    {
        var webhookClient = MemoryStorage.CachedWebhookClients.GetOrCreate(spawnedCharacter);

        if (characterMessage.Length <= 2000)
        {
            await webhookClient.SendMessageAsync(characterMessage);
            return;
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
    }

    #endregion


    #region CreateIntegration

    public static async Task CreateSakuraAiIntegrationAsync(SocketModal modal)
    {
        // Sending mail
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
            throw;
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

    #endregion


    #region Validations

    public static async Task ValidateAccessAsync(AccessLevels requiredAccessLevel, SocketGuildUser user)
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


    private const string DISABLE_WARN_PROMPT = "If these restrictions were imposed intentionally, and you understand how this may affect the bot's operation, then you can disable this warning with `/channel no-warn` command.";

    private static readonly ChannelPermission[] REQUIRED_PERMS = [
        ChannelPermission.ViewChannel, ChannelPermission.SendMessages, ChannelPermission.AddReactions, ChannelPermission.EmbedLinks, ChannelPermission.AttachFiles, ChannelPermission.ManageWebhooks,
        ChannelPermission.CreatePublicThreads, ChannelPermission.CreatePrivateThreads, ChannelPermission.SendMessagesInThreads, ChannelPermission.ManageThreads, ChannelPermission.UseExternalEmojis
    ];

    public static async Task ValidatePermissionsAsync(IGuildChannel channel)
    {
        var botGuildUser = (SocketGuildUser)await channel.GetUserAsync(DI.GetDiscordSocketClient.CurrentUser.Id);
        if (botGuildUser is null)
        {
            throw new UserFriendlyException($"{MessagesTemplates.WARN_SIGN_DISCORD} Bot has no permission to view this channel");
        }

        var botRoles = botGuildUser.Roles;
        if (botRoles.Select(br => br.Permissions).Any(perm => perm.Administrator))
        {
            return;
        }

        await using var db = DatabaseHelper.GetDbContext();
        var noWarn = await db.DiscordChannels.Where(c => c.Id == channel.Id).Select(c => c.NoWarn).FirstAsync();
        if (noWarn)
        {
            return;
        }

        var everyoneRoleId = channel.Guild.EveryoneRole.Id;
        var botChannelPermOws = channel.PermissionOverwrites.Where(BotAffectedByOw).ToList();

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

            throw new UserFriendlyException(msg, false);
        }

        if (prohibitiveOws.Count != 0)
        {
            var msg = $"**{MessagesTemplates.WARN_SIGN_DISCORD} This channel has some prohibitive permission overwrites applied to the bot, which may affect its work:**\n" +
                      $"```{string.Join("\n", prohibitiveOws.Select(ow => $"> {ow.perm:G} | Applied to {ow.target}"))}```\n{DISABLE_WARN_PROMPT}";

            throw new UserFriendlyException(msg, false);
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
                    var role = channel.Guild.Roles.First(g => g.Id == ow.TargetId);
                    return $"role @{role.Name}";
                }

                var user = channel.GetUserAsync(ow.TargetId).GetAwaiter().GetResult();
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
