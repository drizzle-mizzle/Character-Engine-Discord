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
[ValidateAccessLevel(AccessLevels.Manager)]
[ValidateChannelPermissions]
public class GuildCommands : InteractionModuleBase<InteractionContext>
{
    private readonly DiscordSocketClient _discordClient;
    private readonly AppDbContext _db;


    public GuildCommands(DiscordSocketClient discordClient, AppDbContext db)
    {
        _discordClient = discordClient;
        _db = db;
    }



    [SlashCommand("messages-format", "Messages format")]
    public async Task MessagesFormat(MessagesFormatAction action, string? newFormat = null)
    {
        await DeferAsync();

        var message = await InteractionsHelper.SharedMessagesFormatAsync(MessagesFormatTarget.guild, action, Context.Guild.Id, newFormat);

        await FollowupAsync(embed: message.ToInlineEmbed(Color.Green, false));
    }


    [SlashCommand("no-warn", "Disable/enable permissions warning")]
    public async Task NoWarn(bool toggle)
    {
        var guild = await _db.DiscordGuilds.FirstAsync(c => c.Id == Context.Guild.Id);
        guild.NoWarn = toggle;
        await _db.SaveChangesAsync();

        var message = $"{MP.OK_SIGN_DISCORD} Permissions validations were {(toggle ? "disabled" : "enabled")}";
        await FollowupAsync(embed: message.ToInlineEmbed(Color.Orange));
    }


    // [SlashCommand("list-characters", "Show a list of all characters spawned on the whole server")]
    public async Task ListCharacters()
    {
        await RespondAsync(embed: MP.WAIT_MESSAGE);

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
}
