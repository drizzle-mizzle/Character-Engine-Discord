using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands;


public class MiscCommands : InteractionModuleBase<InteractionContext>
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;


    public MiscCommands(AppDbContext db, DiscordSocketClient discordClient)
    {
        _db = db;
        _discordClient = discordClient;
    }


    [SlashCommand("ping", "ping")]
    public async Task Ping()
    {
        await RespondAsync(embed: $":ping_pong: Pong! - {_discordClient.Latency} ms".ToInlineEmbed(Color.Red));
    }


    [SlashCommand("say", "say")]
    public async Task Say(string text)
    {
        await RespondAsync(text);
    }
}
