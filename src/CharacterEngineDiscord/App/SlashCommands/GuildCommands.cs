using System.Text;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using MP = CharacterEngine.App.Helpers.Discord.MessagesTemplates;

namespace CharacterEngine.App.SlashCommands;


[Group("server", "Server-wide settings configuration")]
[ValidateChannelPermissions]
public class GuildCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;


    public GuildCommands(AppDbContext db)
    {
        _db = db;
    }


    [SlashCommand("messages-format", "Messages format")]
    public async Task MessagesFormat(MessagesFormatAction action, string? newFormat = null)
    {
        if (action is not MessagesFormatAction.show)
        {
            await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync();

        string message = default!;

        switch (action)
        {
            case MessagesFormatAction.show:
            {
                message = await InteractionsHelper.GetGuildMessagesFormatAsync(Context.Guild.Id);
                break;
            }
            case MessagesFormatAction.update:
            {
                if (newFormat is null)
                {
                    throw new UserFriendlyException("Specify the new-format parameter");
                }

                message = await InteractionsHelper.UpdateGuildMessagesFormatAsync(Context.Guild.Id, newFormat);
                break;
            }
            case MessagesFormatAction.resetDefault:
            {
                message = await InteractionsHelper.UpdateGuildMessagesFormatAsync(Context.Guild.Id, null);
                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [ValidateAccessLevel(AccessLevels.Manager)]
    [SlashCommand("no-warn", "Disable/enable permissions warning")]
    public async Task NoWarn(bool toggle)
    {
        await DeferAsync();

        var guild = await _db.DiscordGuilds.FirstAsync(c => c.Id == Context.Guild.Id);
        guild.NoWarn = toggle;
        await _db.SaveChangesAsync();

        var message = $"{MP.OK_SIGN_DISCORD} Permissions validations were {(toggle ? "disabled" : "enabled")}";
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Orange));
    }


    // [SlashCommand("list-characters", "Show a list of all characters spawned on the whole server")]
    public async Task ListCharacters()
    {
        await DeferAsync();

        var characters = await DatabaseHelper.GetAllSpawnedCharactersInGuildAsync(Context.Guild.Id);
        if (characters.Count == 0)
        {
            await FollowupAsync(embed: "This server has no spawned characters".ToInlineEmbed(Color.Magenta));
            return;
        }

        // TODO: buttons
        var listEmbed = MessagesHelper.BuildCharactersList(characters.OrderByDescending(c => c.MessagesSent).ToArray(), inGuild: true);

        await FollowupAsync(embed: listEmbed);
    }


    [SlashCommand("list-integrations", "Show a list of all integrations on this server")]
    public async Task ListIntegrations()
    {
        await DeferAsync();

        var integrations = await DatabaseHelper.GetAllIntegrationsInGuildAsync(Context.Guild.Id);
        if (integrations.Count == 0)
        {
            await FollowupAsync(embed: "No integrations were found on this server".ToInlineEmbed(Color.Orange));
            return;
        }

        var characters = await DatabaseHelper.GetAllSpawnedCharactersInGuildAsync(Context.Guild.Id);
        var embed = new EmbedBuilder().WithColor(Color.Gold).WithTitle("Integrations");

        var list = new StringBuilder();

        for (var index = 0; index < integrations.Count; index++)
        {
            var guildIntegration = integrations[index];

            var type = guildIntegration.GetIntegrationType();
            var integrationCharactersCount = characters.Count(c => c.GetIntegrationType() == type);

            var line = $"{index + 1}. **{type.GetIcon()} {type:G}** | ID: `{guildIntegration.Id}` | Spawned characters: `{integrationCharactersCount}`";

            list.AppendLine(line);
        }

        embed.WithDescription(list.ToString());

        await FollowupAsync(embed: embed.Build());
    }
    
    [SlashCommand("ignored-users", "Users whose messages characters cannot read")]
    public async Task BlockedUserCommand(UserAction action, IGuildUser? user = null, string? userId = null, IRole? role = null)
    {
        if (action is not UserAction.show)
        {
            await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync();

        var blockedUsers = await _db.GuildBlockedUsers.Where(u => u.DiscordGuildId == Context.Guild.Id).ToArrayAsync();

        switch (action)
        {
            case UserAction.show:
            {
                var list = new StringBuilder();

                foreach (var blockedUser in blockedUsers)
                {
                    string name;
                    if (blockedUser.IsRole)
                    {
                        name = $"Role {Context.Guild.GetRole(blockedUser.UserOrRoleId)?.Mention ?? "?"}";
                    }
                    else
                    {
                        var guildUser = await Context.Guild.GetUserAsync(blockedUser.UserOrRoleId);
                        name = guildUser?.Mention ?? $"**{blockedUser.UserOrRoleId}**";
                    }
                    
                    var managerUser = await Context.Guild.GetUserAsync(blockedUser.BlockedBy);
                    var managerUserName = managerUser?.Mention ?? $"**{blockedUser.BlockedBy}**";

                    list.AppendLine($"{name} | Blocked by **{managerUserName}** at `{blockedUser.BlockedAt.Humanize()}`");
                }

                var embed = new EmbedBuilder().WithColor(Color.Blue)
                                              .WithTitle($"Ignored users ({blockedUsers.Length})")
                                              .WithDescription(list.ToString());

                await FollowupAsync(embed: embed.Build());
                return;
            }
            case UserAction.clearAll:
            {
                await WatchDog.UnblockGuildUsersAsync(blockedUsers);

                await FollowupAsync(embed: "Ignored users list has been cleared".ToInlineEmbed(Color.Green, bold: true));

                return;
            }
        }

        if (role is null && user is null && userId is null)
        {
            throw new UserFriendlyException("Specify a user or role");
        }

        var isRole = role is not null;
        var blockedUserOrRoleId = role?.Id ?? user?.Id ?? ulong.Parse(userId!);
        var mention = isRole ? $"{role!.Mention} role" : $"User <@{blockedUserOrRoleId}>";
        
        string message;
        switch (action)
        {
            case UserAction.add when blockedUsers.Any(blockedUser => blockedUser.UserOrRoleId == blockedUserOrRoleId):
            {
                throw new UserFriendlyException($"{mention} is already in the ignored users list");
            }
            case UserAction.add:
            {
                await WatchDog.BlockGuildUserAsync(blockedUserOrRoleId, Context.Guild.Id, Context.User.Id, isRole);
                message = $"{mention} was successfully added to the ignored users list";
                
                break;
            }
            case UserAction.remove:
            {
                var blockedUser = blockedUsers.FirstOrDefault(u => u.DiscordGuildId == Context.Guild.Id
                                                                && u.UserOrRoleId == blockedUserOrRoleId);

                if (blockedUser is null)
                {
                    throw new UserFriendlyException($"{mention} is not in the ignored users list");
                }

                await WatchDog.UnblockGuildUserAsync(blockedUser);
                message = $"{mention} was successfully removed from the ignored users list";
                
                break;
            }
            default:
            {
                throw new ArgumentException();
            }
        }
        
        if (isRole)
        {
            await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: true));
        }
        else
        {
            var guildUser = await Context.Guild.GetUserAsync(blockedUserOrRoleId);
            await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, bold: true, imageUrl: guildUser?.GetAvatarUrl(), imageAsThumb: false));
        }
    }
}
