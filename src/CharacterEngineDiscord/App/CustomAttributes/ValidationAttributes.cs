using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.CustomAttributes;


public enum AccessLevels
{
    BotAdmin,
    GuildAdmin,
    Manager
}


public class ValidateAccessLevelAttribute : PreconditionAttribute
{
    private readonly AccessLevels _requiredAccessLevel;

    public ValidateAccessLevelAttribute(AccessLevels accessLevel)
    {
        _requiredAccessLevel = accessLevel;
    }


    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo info, IServiceProvider services)
    {
        if (BotConfig.OWNER_USERS_IDS.Contains(context.User.Id))
        {
            return PreconditionResult.FromSuccess();
        }

        var userIsGuildOwner = UserIsGuildOwner(context);

        switch (_requiredAccessLevel)
        {
            case AccessLevels.BotAdmin:
            {
                throw new UnauthorizedAccessException();
            }
            case AccessLevels.GuildAdmin:
            {
                if (userIsGuildOwner)
                {
                    break;
                }

                throw new UserFriendlyException($"Only server administrators are can access this command.");
            }
            case AccessLevels.Manager:
            {
                if (userIsGuildOwner || await UserIsManagerAsync(context))
                {
                    break;
                }

                throw new UserFriendlyException("Only managers can access this command. Managers can be added by server administrators with `/managers` command.");
            }
            default:
            {
                throw new UnauthorizedAccessException();
            }
        }

        return PreconditionResult.FromSuccess();
    }


    private static Task<bool> UserIsManagerAsync(IInteractionContext context)
    {
        using var db = DatabaseHelper.GetDbContext();
        return db.Managers.AnyAsync(manager => manager.DiscordGuildId == context.Guild.Id
                                            && manager.UserId == context.User.Id);
    }


    private static bool UserIsGuildOwner(IInteractionContext context)
    {
        var currentUser = (SocketGuildUser)context.User;
        return currentUser.Id == context.Guild.OwnerId
            || currentUser.Roles.Any(role => role.Permissions.Administrator);
    }
}


public class DeferAndValidatePermissionsAttribute : PreconditionAttribute
{
    private const string DISABLE_WARN_PROMPT = "If these restrictions were imposed intentionally, and you understand how this may affect the bot's operation, then you can disable this warning with `/channel no-warn` command.";


    private static readonly ChannelPermission[] REQUIRED_PERMS = [
        ChannelPermission.ViewChannel, ChannelPermission.SendMessages, ChannelPermission.AddReactions, ChannelPermission.EmbedLinks, ChannelPermission.AttachFiles, ChannelPermission.ManageWebhooks,
        ChannelPermission.CreatePublicThreads, ChannelPermission.CreatePrivateThreads, ChannelPermission.SendMessagesInThreads, ChannelPermission.ManageThreads, ChannelPermission.UseExternalEmojis
    ];


    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo info, IServiceProvider services)
    {
        if (context.Channel is not ITextChannel channel)
        {
            throw new UserFriendlyException($"{MessagesTemplates.WARN_SIGN_DISCORD} Bot can opearte only in text channels");
        }

        await context.Interaction.DeferAsync();
        await channel.EnsureExistInDbAsync();

        var botGuildUser = (SocketGuildUser)await context.Channel.GetUserAsync(DependencyInjectionHelper.GetDiscordSocketClient.CurrentUser.Id);
        if (botGuildUser is null)
        {
            throw new UserFriendlyException($"{MessagesTemplates.WARN_SIGN_DISCORD} Bot has no permission to view this channel");
        }

        var botRoles = botGuildUser.Roles;
        if (botRoles.Select(br => br.Permissions).Any(perm => perm.Administrator))
        {
            return PreconditionResult.FromSuccess();
        }

        await using var db = DatabaseHelper.GetDbContext();
        var noWarn = await db.DiscordChannels.Where(c => c.Id == context.Channel.Id).Select(c => c.NoWarn).FirstAsync();
        if (noWarn)
        {
            return PreconditionResult.FromSuccess();
        }

        var socketTextChannel = (SocketTextChannel)context.Channel;
        var botChannelPermOws = socketTextChannel.PermissionOverwrites.Where(BotAffectedByOw).ToList();

        var botAllowedPerms = botRoles.SelectMany(ChannelPerms).Concat(botChannelPermOws.SelectMany(AllowedPerms)).ToList();
        var channelDeniedPermOws = botChannelPermOws.Where(ExcludeEveryoneRole).SelectMany(DeniedPerms).ToList();

        var missingPerms = new List<ChannelPermission>();
        var prohibitiveOws = new List<(ChannelPermission perm, string target)>();

        foreach (var requiredPerm in REQUIRED_PERMS)
        {
            if (botAllowedPerms.Contains(requiredPerm))
            {
                prohibitiveOws.AddRange(channelDeniedPermOws.Where(ow => ow.perm == requiredPerm)); // usually will add nothing
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

        return PreconditionGroupResult.FromSuccess();


        #region Shortcuts

        IEnumerable<ChannelPermission> AllowedPerms(Overwrite ow)
            => ow.Permissions.ToAllowList();

        bool ExcludeEveryoneRole(Overwrite ow)
            => ow.TargetId != context.Guild.EveryoneRole.Id;

        IEnumerable<ChannelPermission> ChannelPerms(SocketRole role)
            => role.Permissions.ToList().Cast<ChannelPermission>();

        IEnumerable<(ChannelPermission perm, string target)> DeniedPerms(Overwrite ow)
            => ow.Permissions.ToDenyList().Select(perm => (perm, target: GetFullTarget(ow)));

        string GetFullTarget(Overwrite ow)
        {
            try
            {
                if (ow.TargetType is PermissionTarget.Role)
                {
                    var role = context.Guild.Roles.First(g => g.Id == ow.TargetId);
                    return $"role @{role.Name}";
                }

                var user = (IGuildUser)context.Channel.GetUserAsync(ow.TargetId).GetAwaiter().GetResult();
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

}
