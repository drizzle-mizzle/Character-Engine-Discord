using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers.Discord;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands;


[DeferAndValidatePermissions]
public class MiscCommands : InteractionModuleBase<InteractionContext>
{
    private readonly DiscordSocketClient _discordClient;


    public MiscCommands(DiscordSocketClient discordClient)
    {
        _discordClient = discordClient;
    }


    [SlashCommand("ping", "ping")]
    public async Task Ping()
    {
        await FollowupAsync(embed: $":ping_pong: Pong! - {_discordClient.Latency} ms".ToInlineEmbed(Color.Red));
    }


    [ValidateAccessLevel(AccessLevels.Manager)]
    [SlashCommand("say", "say")]
    public async Task Say(string text)
    {
        await FollowupAsync(text);
    }
}
