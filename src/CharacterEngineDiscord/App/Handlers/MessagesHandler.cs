using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Models;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.App.Handlers;


public class MessagesHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _log;
    private AppDbContext _db { get; set; }

    private readonly DiscordSocketClient _discordClient;


    public MessagesHandler(IServiceProvider serviceProvider, ILogger log, AppDbContext db, DiscordSocketClient discordClient)
    {
        _serviceProvider = serviceProvider;
        _log = log;
        _db = db;

        _discordClient = discordClient;
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
        var message = socketMessage.Content.Trim('\n', ' ');

        var prefixValidation = DoMessageStartsWithPrefix(message);

        if (prefixValidation.Pass)
        {
            var calledCharacter = await DatabaseHelper.GetSpawnedCharacterByIdAsync(prefixValidation.CachedCharacter!.Id);
            if (calledCharacter is null)
            {
                return;
            }

        }
    }


    // Validate and define path

    private static (bool Pass, CachedCharacterInfo? CachedCharacter) DoMessageStartsWithPrefix(string message)
    {
        var character = StaticStorage.CachedCharacters
                                      .ToList()
                                      .FirstOrDefault(c => message.StartsWith(c.CallPrefix, StringComparison.Ordinal));

        return (character is not null, character);
    }
}
