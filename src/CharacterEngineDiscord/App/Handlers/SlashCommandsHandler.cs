using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.SlashCommands.Explicit;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
                await _discordClient.ReportErrorAsync(e, CommonHelper.NewTraceId());
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        if (command.Channel is not IGuildChannel guildChannel)
        {
            return;
        }

        var ensureExistInDbAsync = guildChannel.EnsureExistInDbAsync();
        await InteractionsHelper.ValidateUserAsync(command);

        try
        {
            if (Enum.TryParse<SpecialCommands>(command.CommandName, ignoreCase: false, out var specialCommand))
            {
                var specialCommandsHandler = _serviceProvider.GetRequiredService<SpecialCommandsHandler>();
                await (specialCommand switch
                {
                    SpecialCommands.start => specialCommandsHandler.HandleStartCommandAsync(command),
                    SpecialCommands.disable => specialCommandsHandler.HandleDisableCommandAsync(command),
                });
            }
            else if (Enum.TryParse<BotAdminCommands>(command.CommandName, ignoreCase: false, out var botAdminCommand))
            {
                await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.BotAdmin, (SocketGuildUser)command.User);

                var botAdminCommandsHandler = _serviceProvider.GetRequiredService<BotAdminCommandsHandler>();
                await (botAdminCommand switch
                {
                    BotAdminCommands.shutdown => botAdminCommandsHandler.ShutdownAsync(command),
                    BotAdminCommands.blockUser => botAdminCommandsHandler.BlockUserAsync(command),
                    BotAdminCommands.unblockUser => botAdminCommandsHandler.UnblockUserAsync(command),
                    BotAdminCommands.blockGuild => throw new NotImplementedException(),
                    BotAdminCommands.unblockGuild => throw new NotImplementedException(),
                    BotAdminCommands.stats => throw new NotImplementedException()
                });
            }
            else
            {
                await _interactions.ExecuteCommandAsync(new InteractionContext(_discordClient, command, command.Channel), _serviceProvider);
            }
        }
        finally
        {
            await ensureExistInDbAsync;
        }
    }
}
