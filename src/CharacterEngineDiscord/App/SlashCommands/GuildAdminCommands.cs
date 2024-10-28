using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.SlashCommands;


[ValidateAccessLevel(AccessLevels.GuildAdmin)]
public class GuildAdminCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;
    public enum ManagersActions { add, remove, clearAll }


    public GuildAdminCommands(AppDbContext db, DiscordSocketClient discordClient)
    {
        _db = db;
        _discordClient = discordClient;
    }


    [SlashCommand("managers", "Add or remove managers")]
    [ValidateAccessLevel(AccessLevels.GuildAdmin)]
    public async Task ManagersCommand(ManagersActions action, IGuildUser? user = null, string? userId = null)
    {
        await DeferAsync();

        string message = default!;
        var managers = await _db.GuildBotManagers.Where(manager => manager.DiscordGuildId == Context.Guild.Id).ToListAsync();

        if (action is ManagersActions.clearAll)
        {
            _db.GuildBotManagers.RemoveRange(managers);
            await _db.SaveChangesAsync();

            message = "Managers list has been cleared.";
            await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: true));

            return;
        }

        if (user is null && userId is null)
        {
            throw new UserFriendlyException($"Specify the user to {action:G}");
        }

        var managerUserId = user?.Id ?? ulong.Parse(userId!);
        var guildUser = user ?? await Context.Guild.GetUserAsync(managerUserId);
        var mention = guildUser?.Mention ?? $"User `{managerUserId}`";

        if (action is ManagersActions.add)
        {
            if (managers.Any(manager => manager.UserId == managerUserId))
            {
                throw new UserFriendlyException($"{mention} is already a manager");
            }

            var newManager = new GuildBotManager
            {
                UserId = managerUserId,
                DiscordGuildId = Context.Guild.Id,
                AddedBy = Context.User.Id,
            };

            await _db.GuildBotManagers.AddAsync(newManager);
            message = $"{mention} was successfully added to the managers list.";
        }

        if (action is ManagersActions.remove)
        {
            var manager = managers.First(manager => manager.DiscordGuildId == Context.Guild.Id
                                                 && manager.UserId == managerUserId);

            if (manager is null)
            {
                throw new UserFriendlyException($"{mention} is not a manager, thus cannot be removed from the managers list");
            }

            _db.GuildBotManagers.Remove(manager);
            message = $"{mention} was successfully removed from the managers list.";
        }

        await _db.SaveChangesAsync();
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: true, imageUrl: guildUser?.GetAvatarUrl(), imageAsThumb: false));
    }

}
