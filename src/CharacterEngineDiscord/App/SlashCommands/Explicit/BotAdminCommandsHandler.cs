using CharacterEngineDiscord.Models;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands.Explicit;


public class BotAdminCommandsHandler
{
    private readonly AppDbContext _db;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public BotAdminCommandsHandler(AppDbContext db, DiscordSocketClient discordClient, InteractionService interactions)
    {
        _db = db;
        _discordClient = discordClient;
        _interactions = interactions;
    }
}
