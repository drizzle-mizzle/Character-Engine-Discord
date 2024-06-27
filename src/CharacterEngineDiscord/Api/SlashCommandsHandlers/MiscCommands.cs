using CharacterEngine.Database;
using CharacterEngine.Helpers;
using CharacterEngine.Helpers.Discord;
using CharacterEngine.Models.Db.Discord;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.Api.SlashCommandsHandlers;


public class MiscCommands : InteractionModuleBase<InteractionContext>
{
    public required DiscordSocketClient DiscordClient { get; set; }


    [SlashCommand("ping", "ping")]
    public async Task Ping()
    {
        await RespondAsync(embed: $":ping_pong: Pong! - {DiscordClient.Latency} ms".ToInlineEmbed(Color.Red));
    }
}
