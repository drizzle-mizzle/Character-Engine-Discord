using System.Text;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.SlashCommands;


public class GuildAdminCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    // private readonly DiscordSocketClient _discordClient;


    public GuildAdminCommands(AppDbContext db)
    {
        _db = db;
        // _discordClient = discordClient;
    }


    [SlashCommand("managers", "Users who can use bot's commands")]
    public async Task ManagersCommand(UserAction action, IGuildUser? user = null, string? userId = null, IRole? role = null)
    {
        if (action is not UserAction.show)
        {
            await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.GuildAdmin, (SocketGuildUser)Context.User);
        }

        await DeferAsync();

        var managers = await _db.GuildBotManagers.Where(manager => manager.DiscordGuildId == Context.Guild.Id).ToArrayAsync();

        switch (action)
        {
            case UserAction.show:
            {
                var list = new StringBuilder();

                foreach (var manager in managers)
                {
                    string name;
                    if (manager.IsRole)
                    {
                        name = $"Role {Context.Guild.GetRole(manager.DiscordUserOrRoleId).Mention}";
                    }
                    else
                    {
                        var guildUser = await Context.Guild.GetUserAsync(manager.DiscordUserOrRoleId);
                        name = guildUser?.Mention ?? manager.DiscordUserOrRoleId.ToString();
                    }

                    var addedByGuildUser = await Context.Guild.GetUserAsync(manager.AddedBy) ;
                    list.AppendLine($"{name} | Added by **{addedByGuildUser?.Username ?? manager.AddedBy.ToString()}**");
                }

                var embed = new EmbedBuilder().WithColor(Color.Blue)
                                              .WithTitle($"Managers ({managers.Length})")
                                              .WithDescription(list.ToString());

                await FollowupAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None);
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

        if (role is null && user is null && userId is null)
        {
            throw new UserFriendlyException("Specify a user or role");
        }

        var isRole = role is not null;
        var managerUserOrRoleId = role?.Id ?? user?.Id ?? ulong.Parse(userId!);
        var mention = isRole ? $"{role!.Mention} role" : $"User <@{managerUserOrRoleId}>";

        string message;
        switch (action)
        {
            case UserAction.add when managers.Any(manager => manager.DiscordUserOrRoleId == managerUserOrRoleId):
            {
                throw new UserFriendlyException($"{mention} is already in the managers list");
            }
            case UserAction.add:
            {
                var newManager = new GuildBotManager
                {
                    DiscordUserOrRoleId = managerUserOrRoleId,
                    DiscordGuildId = Context.Guild.Id,
                    AddedBy = Context.User.Id,
                    IsRole = isRole
                };

                _db.GuildBotManagers.Add(newManager);
                message = $"{mention} was successfully added to the managers list";
                break;
            }
            case UserAction.remove:
            {
                var manager = managers.FirstOrDefault(manager => manager.DiscordGuildId == Context.Guild.Id
                                                              && manager.DiscordUserOrRoleId == managerUserOrRoleId);
                if (manager is null)
                {
                    throw new UserFriendlyException($"{mention} is not in the managers list");
                }

                _db.GuildBotManagers.Remove(manager);
                message = $"{mention} was successfully removed from the managers list";
                break;
            }
            default:
            {
                throw new ArgumentException();
            }
        }

        await _db.SaveChangesAsync();

        if (isRole)
        {
            await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: true));
        }
        else
        {
            var guildUser = await Context.Guild.GetUserAsync(managerUserOrRoleId);
            await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: true, imageUrl: guildUser?.GetAvatarUrl(), imageAsThumb: false));
        }
    }
}
