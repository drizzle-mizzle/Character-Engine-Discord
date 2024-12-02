using System.Text;
using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
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

        var message = await InteractionsHelper.SharedMessagesFormatAsync(MessagesFormatTarget.guild, action, Context.Guild.Id, newFormat);

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
}
