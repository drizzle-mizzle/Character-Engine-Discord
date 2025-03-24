using System.Text;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Masters;
using CharacterEngine.App.Repositories;
using CharacterEngine.App.Services;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Shared.Abstractions.Sources.OpenRouter;
using CharacterEngineDiscord.Shared.Helpers;
using CharacterEngineDiscord.Shared.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using static CharacterEngine.App.Helpers.ValidationsHelper;
using MT = CharacterEngine.App.Helpers.Discord.MessagesTemplates;

namespace CharacterEngine.App.Handlers.SlashCommands;


[Group("server", "Server-wide settings configuration")]
[ValidateChannelPermissions]
public class GuildCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly CharactersRepository _charactersRepository;
    private readonly IntegrationsRepository _integrationsRepository;
    private readonly InteractionsMaster _interactionsMaster;


    public GuildCommands(
        AppDbContext db,
        CharactersRepository charactersRepository,
        IntegrationsRepository integrationsRepository,
        InteractionsMaster interactionsMaster
    )
    {
        _db = db;
        _charactersRepository = charactersRepository;
        _integrationsRepository = integrationsRepository;
        _interactionsMaster = interactionsMaster;
    }


    [SlashCommand("messages-format", "Default messages format for all integrations on the server")]
    public async Task MessagesFormat(SinglePropertyAction action, string? newFormat = null)
    {
        if (action is not SinglePropertyAction.show)
        {
            await ValidateAccessLevelAsync(AccessLevel.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync();

        string message = null!;

        switch (action)
        {
            case SinglePropertyAction.show:
            {
                message = "Server-wide messages format:\n" + await _interactionsMaster.BuildGuildMessagesFormatDisplayAsync(Context.Guild.Id);
                break;
            }
            case SinglePropertyAction.update:
            {
                if (newFormat is null)
                {
                    throw new UserFriendlyException("Specify the new-format parameter");
                }

                message = await UpdateGuildMessagesFormatAsync(Context.Guild.Id, newFormat);
                break;
            }
            case SinglePropertyAction.resetDefault:
            {
                message = await UpdateGuildMessagesFormatAsync(Context.Guild.Id, null);
                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [SlashCommand("system-prompt", "Change default system prompt for all integrations on the server")]
    public async Task SystemPrompt(SinglePropertyAction action, string? newPrompt = null)
    {
        if (action is not SinglePropertyAction.show)
        {
            await ValidateAccessLevelAsync(AccessLevel.Manager, (SocketGuildUser)Context.User);
        }

        await DeferAsync();

        string message = null!;

        switch (action)
        {
            case SinglePropertyAction.show:
            {
                message = "Server-wide system prompt:\n" + await _interactionsMaster.BuildGuildSystemPromptDisplayAsync(Context.Guild.Id);
                break;
            }
            case SinglePropertyAction.update:
            {
                if (newPrompt is null)
                {
                    throw new UserFriendlyException("Specify the new-prompt parameter");
                }

                message = await UpdateGuildSystemPromptAsync(Context.Guild.Id, newPrompt);
                break;
            }
            case SinglePropertyAction.resetDefault:
            {
                message = await UpdateGuildSystemPromptAsync(Context.Guild.Id, null);
                break;
            }
        }

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("no-warn", "Disable/enable permissions warning")]
    public async Task NoWarn(bool toggle)
    {
        await DeferAsync();

        var guild = await _db.DiscordGuilds.FirstAsync(c => c.Id == Context.Guild.Id);
        guild.NoWarn = toggle;
        await _db.SaveChangesAsync();

        var message = $"{MT.OK_SIGN_DISCORD} Permissions validations were {(toggle ? "disabled" : "enabled")}";
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Orange));
    }


    // [SlashCommand("list-characters", "Show a list of all characters spawned on the whole server")]
    public async Task ListCharacters()
    {
        await DeferAsync();

        var characters = await _charactersRepository.GetAllSpawnedCharactersInGuildAsync(Context.Guild.Id);
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

        var integrations = await _integrationsRepository.GetAllIntegrationsInGuildAsync(Context.Guild.Id);
        if (integrations.Count == 0)
        {
            await FollowupAsync(embed: "No integrations were found on this server".ToInlineEmbed(Color.Orange));
            return;
        }

        var characters = await _charactersRepository.GetAllSpawnedCharactersInGuildAsync(Context.Guild.Id);
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
            await ValidateAccessLevelAsync(AccessLevel.Manager, (SocketGuildUser)Context.User);
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


    [ValidateAccessLevel(AccessLevel.Manager)]
    [SlashCommand("openrouter-settings", "Display server-wide OpenRouter settings")]
    public async Task OpenRouterSettings()
    {
        var integration = await _db.OpenRouterIntegrations.FirstOrDefaultAsync(i => i.DiscordGuildId == Context.Guild.Id);
        ArgumentNullException.ThrowIfNull(integration);

        var jsonIntegration = JsonConvert.SerializeObject(integration, Formatting.Indented);
        var settings = JsonConvert.DeserializeObject<OpenRouterSettings>(jsonIntegration);
        var jsonSettings = JsonConvert.SerializeObject(settings, Formatting.Indented);

        var customId = InteractionsHelper.NewCustomId(ModalActionType.OpenRouterSettings, $"{integration.Id}~{SettingTarget.Guild:D}");
        var modal = new ModalBuilder().WithTitle("Edit server-wide OpenRouter settings")
                                      .WithCustomId(customId)
                                      .AddTextInput("Settings:", "settings", TextInputStyle.Paragraph, value: jsonSettings)
                                      .Build();

        await RespondWithModalAsync(modal); // next in EnsureSakuraAiLoginAsync()
    }

    private async Task<string> UpdateGuildMessagesFormatAsync(ulong guildId, string? newFormat)
    {
        ValidateMessagesFormat(newFormat);

        var guild = await _db.DiscordGuilds.FindAsync(guildId);
        ArgumentNullException.ThrowIfNull(guild);

        guild.MessagesFormat = newFormat;
        await _db.SaveChangesAsync();

        return $"Server-wide messages format {(newFormat is null ? "reset to default value" : "was changed")} successfully:\n" + _interactionsMaster.BuildGuildMessagesFormatDisplayAsync(guild);
    }


    private async Task<string> UpdateGuildSystemPromptAsync(ulong guildId, string? newPrompt)
    {
        // ValidateMessagesFormat(newFormat);

        var guild = await _db.DiscordGuilds.FirstAsync(g => g.Id == guildId);
        guild.SystemPrompt = newPrompt;
        await _db.SaveChangesAsync();

        return $"Server-wide system prompt {(newPrompt is null ? "reset to default value" : "was changed")} successfully:\n" + _interactionsMaster.BuildGuildSystemPromptDisplayAsync(guild);
    }
}
