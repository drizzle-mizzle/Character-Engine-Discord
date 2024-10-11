using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands;


public class MiscCommands : InteractionModuleBase<InteractionContext>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public MiscCommands(IServiceProvider serviceProvider, AppDbContext db, DiscordSocketClient discordClient, InteractionService interactions)
    {
        _serviceProvider = serviceProvider;
        _db = db;

        _discordClient = discordClient;
        _interactions = interactions;
    }


    [SlashCommand("ping", "ping")]
    public async Task Ping()
    {
        await RespondAsync(embed: $":ping_pong: Pong! - {_discordClient.Latency} ms".ToInlineEmbed(Color.Red));
    }
}
