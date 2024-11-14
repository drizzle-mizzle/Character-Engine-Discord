﻿using CharacterEngine.App.CustomAttributes;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.SlashCommands.Explicit;
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
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactionService;


    public SlashCommandsHandler(IServiceProvider serviceProvider, DiscordSocketClient discordClient, InteractionService interactionService)
    {
        _serviceProvider = serviceProvider;
        _discordClient = discordClient;
        _interactionService = interactionService;
    }


    public Task HandleSlashCommand(SocketSlashCommand command)
    {
        Task.Run(async () =>
        {
            await HandleSlashCommandAsync(command);
        });

        return Task.CompletedTask;
    }


    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        if (command.Channel is not IGuildChannel guildChannel)
        {
            return;
        }

        var ensureChannelExistInDbAsync = guildChannel.EnsureExistInDbAsync();
        try
        {
            InteractionsHelper.ValidateUser(command);

            var context = new InteractionContext(_discordClient, command, command.Channel);

            if (Enum.TryParse<SpecialCommands>(command.CommandName, ignoreCase: false, out var specialCommand))
            {
                await InteractionsHelper.ValidateAccessLevelAsync(AccessLevels.GuildAdmin, (SocketGuildUser)command.User);

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
                await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
            }
        }
        catch (Exception e)
        {
            await _discordClient.ReportErrorAsync(e, CommonHelper.NewTraceId(), false);
        }
        finally
        {
            await ensureChannelExistInDbAsync;
        }
    }
}
