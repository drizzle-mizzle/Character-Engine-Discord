using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Handlers.SlashCommands.Explicit;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Repositories;
using CharacterEngine.App.Services;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Db;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using static CharacterEngine.App.Helpers.ValidationsHelper;

namespace CharacterEngine.App.Handlers;


public class SlashCommandsHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactionService;
    // private readonly CharactersRepository _charactersRepository;
    private readonly CacheRepository _cacheRepository;


    public SlashCommandsHandler(
        IServiceProvider serviceProvider,
        DiscordSocketClient discordClient,
        InteractionService interactionService,
        // CharactersRepository charactersRepository,
        CacheRepository cacheRepository
    )
    {
        _serviceProvider = serviceProvider;
        _discordClient = discordClient;
        _interactionService = interactionService;
        // _charactersRepository = charactersRepository;
        _cacheRepository = cacheRepository;
    }


    public Task HandleSlashCommand(SocketSlashCommand command)
    {
        Task.Run(async () =>
        {
            try
            {
                await HandleSlashCommandAsync(command);
            }
            catch (OutOfMemoryException)
            {
                Environment.Exit(666);
            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException or UserFriendlyException)
                {
                    return;
                }

                await _discordClient.ReportErrorAsync("⌨SlashCommandsHandler Exception", null, e, CommonHelper.NewTraceId(), false);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        if (command.Channel is not ITextChannel textChannel)
        {
            return;
        }

        var guildUser = (IGuildUser)command.User;

        _ = _cacheRepository.EnsureChannelCached(textChannel);
        _ = _cacheRepository.EnsureUserCached(guildUser);

        ValidateInteraction(guildUser, textChannel);

        var commandNameCamel = command.CommandName.Replace("-", "");
        if (Enum.TryParse<SpecialCommands>(commandNameCamel, ignoreCase: true, out var specialCommand))
        {
            await ValidateAccessLevelAsync(AccessLevel.GuildAdmin, (SocketGuildUser)command.User);

            var specialCommandsHandler = _serviceProvider.GetRequiredService<SpecialCommandsHandler>();
            await (specialCommand switch
            {
                SpecialCommands.start => specialCommandsHandler.HandleStartCommandAsync(command),
                SpecialCommands.disable => specialCommandsHandler.HandleDisableCommandAsync(command),
            });
        }
        else if (Enum.TryParse<BotAdminCommands>(commandNameCamel, ignoreCase: true, out var botAdminCommand))
        {
            await ValidateAccessLevelAsync(AccessLevel.BotAdmin, (SocketGuildUser)command.User);

            var botAdminCommandsHandler = _serviceProvider.GetRequiredService<BotAdminCommandsHandler>();
            await (botAdminCommand switch
            {
                BotAdminCommands.shutdown => botAdminCommandsHandler.ShutdownAsync(command),
                BotAdminCommands.blockUser => botAdminCommandsHandler.BlockUserAsync(command),
                BotAdminCommands.unblockUser => botAdminCommandsHandler.UnblockUserAsync(command),
                BotAdminCommands.blockGuild => throw new NotImplementedException(),
                BotAdminCommands.unblockGuild => throw new NotImplementedException(),
                BotAdminCommands.reportMetrics => botAdminCommandsHandler.ReportMetricsAsync(command)
            });
        }
        else
        {
            var context = new InteractionContext(_discordClient, command, textChannel);
            var result = await _interactionService.ExecuteCommandAsync(context, _serviceProvider);

            string commandName;
            string options;
            
            if (command.Data.Options.Any(opt => opt.Type is ApplicationCommandOptionType.SubCommand))
            {
                var subCommand = command.Data.Options.First();
                
                commandName = $"{command.CommandName}/{subCommand.Name}";
                options = string.Join(" | ", subCommand.Options.Select(opt => $"{opt.Name}: {opt.Value}"));
            }
            else if (command.Data.Options.Any(opt => opt.Type is ApplicationCommandOptionType.SubCommandGroup))
            {
                var subCommandGroup = command.Data.Options.First();
                var subCommand = subCommandGroup.Options.First();

                commandName = $"{command.CommandName}/{subCommandGroup.Name}/{subCommand.Name}";
                options = string.Join(" | ", subCommand.Options.Select(opt => $"{opt.Name}: {opt.Value}"));
            }
            else
            {
                commandName = command.CommandName;
                options = string.Join(" | ", command.Data.Options.Select(opt => $"{opt.Name}: {opt.Value}"));
            }
            
            MetricsWriter.Write(MetricType.NewInteraction, guildUser.Id, $"{MetricUserSource.SlashCommand:G}:{commandName}:{(result.IsSuccess ? "ok" : "err")}:{textChannel.Id}:{textChannel.GuildId}: [ {options} ]", true);
        }

    }
}
