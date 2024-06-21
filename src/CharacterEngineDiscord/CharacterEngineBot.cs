using System.Diagnostics;
using System.Reflection;
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

public class CharacterEngineBot
{
    public static void Run()
        => new CharacterEngineBot().RunAsync().GetAwaiter().GetResult();

    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly IServiceProvider _serviceProvider = ServiceCollectionHelper.BuildServiceProvider();
    private static readonly DiscordSocketClient _discordClient = _serviceProvider.GetRequiredService<DiscordSocketClient>();
    private static readonly InteractionService _interactionService = _serviceProvider.GetRequiredService<InteractionService>();

    private bool _firstLaunch = true;
    private RestApplication APP_INFO;

    private async Task RunAsync()
    {
        HandlerBase.Inject(_serviceProvider);

        await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider).ConfigureAwait(false);
        _discordClient.BindEvents(_serviceProvider);
        _discordClient.Connected += OnConnected;

        await _discordClient.LoginAsync(TokenType.Bot, BotConfig.BOT_TOKEN).ConfigureAwait(false);
        await _discordClient.StartAsync().ConfigureAwait(false);

        RunJobs();

        await Task.Delay(-1);
    }


    private void RunJobs()
    {
        Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed.Seconds >= 3)
            {
                await RunQuickJobsAsync();
                sw.Restart();
            }
        });

        Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed.Minutes >= 1)
            {
                await RunQuickJobsAsync();
                sw.Restart();
            }
        });
    }


    private async Task RunQuickJobsAsync()
    {
        try
        {
            await using var db = new AppDbContext();
            var storedActions = await db.StoredActions.ToListAsync();

            foreach (var action in storedActions)
            {
                try
                {
                    switch (action.StoredActionType)
                    {
                        case StoredActionType.SakuraAiEnsureLogin:
                            await EnsureSakuraAiAuths(action);
                            break;

                        default: continue;
                    }
                }
                catch (Exception e)
                {
                    await _discordClient.ReportErrorAsync("Exception in Quick Jobs foreach sub-loop", e);
                }
            }
        }
        catch (Exception e)
        {
            await _discordClient.ReportErrorAsync("Exception in Quick Jobs loop", e);
        }
        finally
        {
            await Task.Delay(2000);
        }
    }


    private async Task EnsureSakuraAiAuths(StoredAction action)
    {
        var sakuraAiClient = _serviceProvider.GetRequiredService<SakuraAiClient>();
        var data = StoredActionsHelper.ParseSakuraAiEnsureLoginData(action.Data);
        var result = await sakuraAiClient.EnsureLoginByEmailAsync(data.SignInAttempt);
        if (result is null)
        {
            return;
        }

        await using var db = new AppDbContext();
        db.StoredActions.Remove(action);
        await db.SaveChangesAsync();

        var channel = (ITextChannel)await _discordClient.GetChannelAsync(data.ChannelId);
        // var user = await channel.GetUserAsync(data.UserId);

        await channel.SendMessageAsync(embed: $"✅ SakuraAI user authorized - {result.Username}".ToInlineEmbed(Color.Green, imageUrl: result.UserImageUrl));
    }


    private async Task OnConnected()
    {
        if (!_firstLaunch)
        {
            return;
        }

        _log.Info($"{_discordClient.CurrentUser.Username} - RUNNING");

        APP_INFO = await _discordClient.GetApplicationInfoAsync();

        _log.Info($"Registering /start command to ({_discordClient.Guilds.Count}) guilds...");
        await Parallel.ForEachAsync(_discordClient.Guilds, EnsureBasicCommandsInGuildsAsync).ConfigureAwait(false);
        _log.Info("Commands registered successfully");

        if (BotConfig.PLAYING_STATUS.Length != 0)
        {
            await _discordClient.SetGameAsync(BotConfig.PLAYING_STATUS);
            _log.Info($"Playing status - {BotConfig.PLAYING_STATUS}");
        }

        _firstLaunch = false;
    }

    private async ValueTask EnsureBasicCommandsInGuildsAsync(SocketGuild guild, CancellationToken _)
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
            _log.Error($"Failed to register command in guild {guild.Name} ({guild.Id}): {e}");
        }
    }

}
