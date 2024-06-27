using System.Diagnostics;
using System.Reflection;
using CharacterEngine.Abstractions;
using CharacterEngine.Api.Abstractions;
using CharacterEngine.Database;
using CharacterEngine.Helpers;
using CharacterEngine.Helpers.Common;
using CharacterEngine.Helpers.Discord;
using CharacterEngine.Models.Db;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SakuraAi;

namespace CharacterEngine;

public class CharacterEngineBot : CharacterEngineBase
{
    private RestApplication APP_INFO;
    private bool _firstLaunch = true;

    public static void Run()
        => new CharacterEngineBot().RunAsync().GetAwaiter().GetResult();

    private async Task RunAsync()
    {
        HandlerBase.Inject(Services);

        await Interactions.AddModulesAsync(Assembly.GetEntryAssembly(), Services).ConfigureAwait(false);
        DiscordClient.BindEvents(Services);
        DiscordClient.Connected += OnConnected;

        await DiscordClient.LoginAsync(TokenType.Bot, BotConfig.BOT_TOKEN).ConfigureAwait(false);
        await DiscordClient.StartAsync().ConfigureAwait(false);

        Worker.Run();

        await Task.Delay(-1);
    }


    private async Task OnConnected()
    {
        if (!_firstLaunch)
        {
            return;
        }

        log.Info($"{DiscordClient.CurrentUser.Username} - RUNNING");

        APP_INFO = await DiscordClient.GetApplicationInfoAsync();

        log.Info($"Registering /start command to ({DiscordClient.Guilds.Count}) guilds...");
        await Parallel.ForEachAsync(DiscordClient.Guilds, EnsureBasicCommandsInGuildsAsync).ConfigureAwait(false);
        log.Info("Commands registered successfully");

        if (BotConfig.PLAYING_STATUS.Length != 0)
        {
            await DiscordClient.SetGameAsync(BotConfig.PLAYING_STATUS);
            log.Info($"Playing status - {BotConfig.PLAYING_STATUS}");
        }

        _firstLaunch = false;
        return;

        async ValueTask EnsureBasicCommandsInGuildsAsync(SocketGuild guild, CancellationToken _)
        {
            try
            {
                var guildCommands = await guild.GetApplicationCommandsAsync();

                if (guildCommands.Any(command => command.ApplicationId == APP_INFO.Id))
                {
                    return;
                }

                var startCommand = DiscordCommandsHelper.BuildStartCommand();
                await guild.CreateApplicationCommandAsync(startCommand).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                log.Error($"Failed to register command in guild {guild.Name} ({guild.Id}): {e}");
            }
        }

    }

}
