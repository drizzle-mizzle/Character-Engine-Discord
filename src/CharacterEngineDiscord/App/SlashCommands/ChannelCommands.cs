using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.SlashCommands;


[DeferAndValidatePermissions]
[ValidateAccessLevel(AccessLevels.Manager)]
[Group("channel", "Configure per-channel settings")]
public class ChannelCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;


    public ChannelCommands(AppDbContext db, DiscordSocketClient discordClient)
    {
        _db = db;
        _discordClient = discordClient;
    }


    [SlashCommand("no-warn", "Disable/enable permissions warning")]
    public async Task NoWarn(bool toggle)
    {
        var channel = await _db.DiscordChannels.FirstAsync(c => c.Id == Context.Channel.Id);
        channel.NoWarn = toggle;
        await _db.SaveChangesAsync();

        await FollowupAsync($"{MessagesTemplates.OK_SIGN_DISCORD} {(toggle ? "Disabled" : "Enabled")} permissions checks for this channel");
    }
}
