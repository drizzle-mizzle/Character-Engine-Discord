using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Repositories;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using MT = CharacterEngine.App.Helpers.Discord.MessagesTemplates;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;

namespace CharacterEngine.App.Helpers.Discord;


public static class ValidationsHelper
{

    public enum AccessLevel
    {
        BotAdmin,
        GuildAdmin,
        Manager
    }


    public static async Task<bool> UserIsManagerAsync(SocketGuildUser user)
    {
        var checkedIds = user.Roles.Select(r => r.Id).Append(user.Id).ToArray();

        // TODO: Cache
        await using var db = new AppDbContext(BotConfig.DATABASE_CONNECTION_STRING);
        return await db.GuildBotManagers
                       .Where(manager => manager.DiscordGuildId == user.Guild.Id)
                       .Select(manager => manager.DiscordUserOrRoleId) // ids of all manager-users and manager-roles
                       .AnyAsync(managerId => checkedIds.Contains(managerId));
    }


    public static async Task ValidateAccessLevelAsync(AccessLevel requiredAccessLevel, SocketGuildUser user)
    {
        if (BotConfig.OWNER_USERS_IDS.Contains(user.Id))
        {
            return;
        }

        var userIsGuildAdmin = user.Id == user.Guild.OwnerId || user.Roles.Any(role => role.Permissions.Administrator);

        switch (requiredAccessLevel)
        {
            case AccessLevel.BotAdmin:
            {
                throw new UnauthorizedAccessException();
            }
            case AccessLevel.GuildAdmin:
            {
                if (userIsGuildAdmin)
                {
                    return;
                }

                throw new UserFriendlyException("Only server administrators are allowed to access this command.");
            }
            case AccessLevel.Manager:
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


    public static void ValidateInteraction(IGuildUser user, ITextChannel channel)
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


    public static void ValidateMessagesFormat(string? newFormat)
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

        if (CacheRepository.GetCachedChannelNoWarnState(channel.Id))
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
}
