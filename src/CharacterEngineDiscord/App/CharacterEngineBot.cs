using System.Collections.Concurrent;
using System.Reflection;
using CharacterEngine.App.Handlers;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Common;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace CharacterEngine.App;


public static class CharacterEngineBot
{
    private static readonly IServiceProvider _services = CommonHelper.BuildServiceProvider();
    private static readonly DiscordSocketClient _discordClient = _services.GetRequiredService<DiscordSocketClient>();
    private static readonly InteractionService _interactions = _services.GetRequiredService<InteractionService>();
    private static readonly Logger _log = (_services.GetRequiredService<ILogger>() as Logger)!;



    public static void Run()
        => RunAsync().GetAwaiter().GetResult();


    private static async Task RunAsync()
    {
        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services).ConfigureAwait(false);
        _discordClient.BindEvents();
        _discordClient.Connected += OnConnected;

        await _discordClient.LoginAsync(TokenType.Bot, BotConfig.BOT_TOKEN).ConfigureAwait(false);
        await _discordClient.StartAsync().ConfigureAwait(false);

        var db = _services.GetRequiredService<AppDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        // await db.Database.MigrateAsync();


        await Task.Delay(-1);
    }


    private static async Task OnConnected()
    {
        var guildsToStart = await GetGuildsWithNoCommandsAsync();
        if (guildsToStart.Length != 0)
        {
            _log.Info($"Registering /start command to ({guildsToStart.Length}) guilds...");

            await guildsToStart.RegisterStartCommandAsync();

            _log.Info("Commands registered successfully");
        }

        await Parallel.ForEachAsync(_discordClient.Guilds, async (guild, _) =>
        {
            try
            {
                var commands = await guild.GetApplicationCommandsAsync();
                if (commands.Select(c => c.Name).Contains("disable") == false)
                {
                    var disableCommand = InteractionsHelper.BuildDisableCommand();
                    await guild.CreateApplicationCommandAsync(disableCommand);
                }

                await _interactions.RegisterCommandsToGuildAsync(guild.Id, false);
            }
            catch (Exception e)
            {
                await _discordClient.ReportErrorAsync($"Failed to register slash commands to guild {guild.Name} ({guild.Id})", e);
            }
        });

        if (BotConfig.PLAYING_STATUS.Length != 0)
        {
            await _discordClient.SetGameAsync(BotConfig.PLAYING_STATUS);
            _log.Info($"Playing status - {BotConfig.PLAYING_STATUS}");
        }

        await _discordClient.ReportLogAsync($"{_discordClient.CurrentUser.Username} - Online", null);
        BackgroundWorker.Run(_services);
    }


    private static async Task<SocketGuild[]> GetGuildsWithNoCommandsAsync()
    {
        ConcurrentBag<SocketGuild> guilds = [];
        await Parallel.ForEachAsync(_discordClient.Guilds, async (guild, _) =>
        {
            try
            {
                var guildCommands = await guild.GetApplicationCommandsAsync();
                if (guildCommands.Count == 0)
                {
                    guilds.Add(guild);
                }
            }
            catch
            {
                // nvm
            }
        });

        return guilds.ToArray();
    }


    private static Task RegisterStartCommandAsync(this SocketGuild[] guilds)
    {
        return Parallel.ForEachAsync(guilds, async (guild, _) =>
        {
            try
            {
                var startCommand = InteractionsHelper.BuildStartCommand();
                await guild.CreateApplicationCommandAsync(startCommand).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _log.Error($"Failed to register command in guild {guild.Name} ({guild.Id}): {e}");
            }
        });
    }


    private static void BindEvents(this DiscordSocketClient discordClient)
    {
        discordClient.Log += msg =>
        {
            if (msg.Severity is LogSeverity.Error or LogSeverity.Critical)
            {
                _log.Error(msg.ToString());
            }
            else
            {
                _log.Info(msg.ToString());
            }

            return Task.CompletedTask;
        };

        discordClient.JoinedGuild += guild =>
        {
            _log.Info($"Joined guild {guild.Name} ({guild.Id}) | Members: {guild.MemberCount} | Description: {guild.Description ?? "none"}");
            return Task.CompletedTask;
        };

        discordClient.LeftGuild += guild =>
        {
            _log.Info($"Left guild {guild.Name} ({guild.Id}) | Members: {guild.MemberCount} | Description: {guild.Description ?? "none"}");
            return Task.CompletedTask;
        };

        var interactionService = _services.GetRequiredService<InteractionService>();

        interactionService.InteractionExecuted += _services.GetRequiredService<InteractionsHandler>().HandleInteractionAsync;
        discordClient.SlashCommandExecuted += _services.GetRequiredService<SlashCommandsHandler>().HandleSlashCommandAsync;
        discordClient.ModalSubmitted += _services.GetRequiredService<ModalsHandler>().HandleModalAsync;
        discordClient.ButtonExecuted += _services.GetRequiredService<ButtonsHandler>().HandleButtonAsync;

    }
}
