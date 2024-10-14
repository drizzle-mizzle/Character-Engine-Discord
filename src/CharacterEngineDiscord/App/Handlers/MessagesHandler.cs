using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord.Interactions;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.App.Handlers;


public class MessagesHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _log;
    private AppDbContext _db { get; set; }

    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public MessagesHandler(IServiceProvider serviceProvider, ILogger log, AppDbContext db, DiscordSocketClient discordClient, InteractionService interactions)
    {
        _serviceProvider = serviceProvider;
        _log = log;
        _db = db;

        _discordClient = discordClient;
        _interactions = interactions;
    }


    public Task HandleMessage(SocketMessage socketMessage)
        => Task.Run(async () =>
        {
            try
            {
                await HandleMessageAsync(socketMessage);
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync(e);
            }
        });


    private async Task HandleMessageAsync(SocketMessage socketMessage)
    {

    }
}
