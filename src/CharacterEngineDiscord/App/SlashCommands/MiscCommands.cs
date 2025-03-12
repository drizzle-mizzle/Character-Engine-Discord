using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers.Discord;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands;

[Group("misc", "MISC")]
public class MiscCommands : InteractionModuleBase<InteractionContext>
{
    private readonly DiscordSocketClient _discordClient;


    public MiscCommands(DiscordSocketClient discordClient)
    {
        _discordClient = discordClient;
    }


    [ValidateChannelPermissions]
    [SlashCommand("ping", "ping")]
    public async Task Ping()
    {
        await RespondAsync(embed: $":ping_pong: Pong! - {_discordClient.Latency} ms".ToInlineEmbed(Color.Red));
    }

}
