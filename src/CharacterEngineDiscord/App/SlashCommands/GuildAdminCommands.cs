using System.Text;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.SlashCommands;


[ValidateAccessLevel(AccessLevels.GuildAdmin)]
public class GuildAdminCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    // private readonly DiscordSocketClient _discordClient;


    public GuildAdminCommands(AppDbContext db)
    {
        _db = db;
        // _discordClient = discordClient;
    }


    [SlashCommand("managers", "Add or remove managers")]
    [ValidateAccessLevel(AccessLevels.GuildAdmin)]
    public async Task ManagersCommand(UserAction action, IGuildUser? user = null, string? userId = null)
    {
        await DeferAsync();

        var managers = await _db.GuildBotManagers.Where(manager => manager.DiscordGuildId == Context.Guild.Id).ToListAsync();

        switch (action)
        {
            case UserAction.show:
            {
                var list = new StringBuilder();

                foreach (var manager in managers)
                {
                    var managerUser = await Context.Guild.GetUserAsync(manager.UserId);
                    var addedByUser = await Context.Guild.GetUserAsync(manager.AddedBy);

                    list.AppendLine($"**{managerUser.DisplayName ?? managerUser.Username}** | Added by **{addedByUser.DisplayName ?? addedByUser.Username}**");
                }

                var embed = new EmbedBuilder().WithColor(Color.Blue)
                                              .WithTitle($"Managers ({managers.Count})")
                                              .WithDescription(list.ToString());

                await FollowupAsync(embed: embed.Build());
                return;
            }
            case UserAction.clearAll:
            {
                _db.GuildBotManagers.RemoveRange(managers);
                await _db.SaveChangesAsync();

                await FollowupAsync(embed: "Managers list has been cleared".ToInlineEmbed(Color.Green, bold: true));
                return;
            }
        }

        if (user is null && userId is null)
        {
            throw new UserFriendlyException($"Specify the user to {action:G}");
        }

        var managerUserId = user?.Id ?? ulong.Parse(userId!);
        var guildUser = user ?? await Context.Guild.GetUserAsync(managerUserId);
        var mention = guildUser?.Mention ?? $"User `{managerUserId}`";

        string message = default!;

        switch (action)
        {
            case UserAction.add when managers.Any(manager => manager.UserId == managerUserId):
            {
                throw new UserFriendlyException($"{mention} is already a manager");
            }
            case UserAction.add:
            {
                var newManager = new GuildBotManager
                {
                    UserId = managerUserId,
                    DiscordGuildId = Context.Guild.Id,
                    AddedBy = Context.User.Id,
                };

                await _db.GuildBotManagers.AddAsync(newManager);
                message = $"{mention} was successfully added to the managers list.";
                break;
            }
            case UserAction.remove:
            {
                var manager = managers.First(manager => manager.DiscordGuildId == Context.Guild.Id
                                                     && manager.UserId == managerUserId);

                if (manager is null)
                {
                    throw new UserFriendlyException($"{mention} is not a manager");
                }

                _db.GuildBotManagers.Remove(manager);
                message = $"{mention} was successfully removed from the managers list.";
                break;
            }
        }

        await _db.SaveChangesAsync();
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: true, imageUrl: guildUser?.GetAvatarUrl(), imageAsThumb: false));
    }


    [SlashCommand("ignored-users", "Add or remove ignored users")]
    public async Task BlockedUserCommand(UserAction action, IGuildUser? user = null, string? userId = null)
    {
        await DeferAsync();

        var blockedUsers = await _db.GuildBlockedUsers.Where(u => u.DiscordGuildId == Context.Guild.Id).ToListAsync();

        switch (action)
        {
            case UserAction.show:
            {
                var list = new StringBuilder();

                foreach (var blockedUser in blockedUsers)
                {
                    var guildBlockedUser = await Context.Guild.GetUserAsync(blockedUser.UserId);
                    var blockedUserName = guildBlockedUser.DisplayName ?? guildBlockedUser.Username;
                    var managerUser = await Context.Guild.GetUserAsync(blockedUser.BlockedBy);
                    var managerUserName = managerUser.DisplayName ?? managerUser.Username;

                    list.AppendLine($"**{blockedUserName}** | Blocked by **{managerUserName}** at `{blockedUser.BlockedAt.HumanizeDateTime()}`");
                }

                var embed = new EmbedBuilder().WithColor(Color.Blue)
                                              .WithTitle($"Ignored users ({blockedUsers.Count})")
                                              .WithDescription(list.ToString());

                await FollowupAsync(embed: embed.Build());
                return;
            }
            case UserAction.clearAll:
            {
                _db.GuildBlockedUsers.RemoveRange(blockedUsers);
                await _db.SaveChangesAsync();

                await FollowupAsync(embed: "Blocked users list has been cleared".ToInlineEmbed(Color.Green, bold: true));

                return;
            }
        }

        if (user is null && userId is null)
        {
            throw new UserFriendlyException($"Specify the user to {action:G}");
        }

        var blockUserId = user?.Id ?? ulong.Parse(userId!);
        var guildUser = user ?? await Context.Guild.GetUserAsync(blockUserId);
        var mention = guildUser?.Mention ?? $"User `{blockedUsers}`";

        string message = default!;

        switch (action)
        {
            case UserAction.add when blockedUsers.Any(manager => manager.UserId == blockUserId):
            {
                throw new UserFriendlyException($"{mention} is already blocked");
            }
            case UserAction.add:
            {
                var newManager = new GuildBotManager
                {
                    UserId = blockUserId,
                    DiscordGuildId = Context.Guild.Id,
                    AddedBy = Context.User.Id,
                };

                await _db.GuildBotManagers.AddAsync(newManager);
                message = $"{mention} was successfully added to the block list.";
                break;
            }
            case UserAction.remove:
            {
                var blockedUser = blockedUsers.First(u => u.DiscordGuildId == Context.Guild.Id
                                                       && u.UserId == blockUserId);

                if (blockedUser is null)
                {
                    throw new UserFriendlyException($"{mention} is not blocked");
                }

                _db.GuildBlockedUsers.Remove(blockedUser);
                message = $"{mention} was successfully removed from the bloks list.";
                break;
            }
        }

        await _db.SaveChangesAsync();
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: true, imageUrl: guildUser?.GetAvatarUrl(), imageAsThumb: false));
    }
}
