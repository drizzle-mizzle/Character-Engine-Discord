using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.SlashCommands.Explicit;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace CharacterEngine.App.Handlers;


public class SlashCommandsHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _log;

    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;
    private readonly AppDbContext _db;


    public SlashCommandsHandler(IServiceProvider serviceProvider, ILogger log, DiscordSocketClient discordClient, InteractionService interactions, AppDbContext db)
    {
        _serviceProvider = serviceProvider;
        _log = log;

        _discordClient = discordClient;
        _interactions = interactions;
        _db = db;
    }


    public Task HandleSlashCommand(SocketSlashCommand command)
    {
        Task.Run(async () =>
        {
            try
            {
                await HandleSlashCommandAsync(command);
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync(e);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        if (Enum.TryParse<ExplicitCommands>(command.CommandName, ignoreCase: false, out var adminCommand))
        {
            var specialCommandsHandler = _serviceProvider.GetRequiredService<SpecialCommandsHandler>();
            await (adminCommand switch
            {
                ExplicitCommands.start => specialCommandsHandler.HandleStartCommandAsync(command),
                ExplicitCommands.disable => specialCommandsHandler.HandleDisableCommandAsync(command),
            });
        }
        else
        {
            await _interactions.ExecuteCommandAsync(new InteractionContext(_discordClient, command, command.Channel), _serviceProvider);
        }
    }
}
