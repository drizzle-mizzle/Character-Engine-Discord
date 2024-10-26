using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using DI = CharacterEngine.App.Helpers.Infrastructure.DependencyInjectionHelper;

namespace CharacterEngine.App;


public class CharacterEngineBot
{
    private InteractionService _interactions = null!;
    private DiscordSocketClient _discordClient = null!;
    private ILogger _log = null!;


    public static void Run()
        => new CharacterEngineBot().RunAsync().GetAwaiter().GetResult();


    private async Task RunAsync()
    {
        var serviceProvider = DI.BuildServiceProvider();
        _discordClient = serviceProvider.GetRequiredService<DiscordSocketClient>();
        _interactions = serviceProvider.GetRequiredService<InteractionService>();
        _log = serviceProvider.GetRequiredService<ILogger>();

        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);

        // Cache all characters from db
        _ = Task.Run(async () =>
        {
            var allCharacters = await DatabaseHelper.GetAllSpawnedCharactersAsync();
            StaticStorage.CachedCharacters.AddRange(allCharacters);
            _log.Info($"Cached {allCharacters.Count} characters");
        });

        // Configure event handlers
        BindEvents();

        // Connect
        await _discordClient.LoginAsync(TokenType.Bot, BotConfig.BOT_TOKEN);

        // Launch the bot
        await _discordClient.StartAsync();

        // Prevent application from closing
        await Task.Delay(-1);
    }


    private void BindEvents()
    {
        AppDomain.CurrentDomain.UnhandledException += async (_, e) =>
        {
            await _discordClient.ReportErrorAsync("UnhandledException", e.ExceptionObject.ToString() ?? "");
        };

        TaskScheduler.UnobservedTaskException += async (_, e) =>
        {
            await _discordClient.ReportErrorAsync("UnobservedTaskException", e.Exception.ToString());
        };

        _discordClient.Log += msg =>
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

        _discordClient.JoinedGuild += guild =>
        {
            _log.Info($"Joined guild {guild.Name} ({guild.Id}) | Members: {guild.MemberCount} | Description: {guild.Description ?? "none"}");
            _ = guild.EnsureExistInDbAsync();

            return Task.CompletedTask;
        };

        _discordClient.LeftGuild += guild =>
        {
            _log.Info($"Left guild {guild.Name} ({guild.Id}) | Members: {guild.MemberCount} | Description: {guild.Description ?? "none"}");
            _ = guild.MarkAsLeftAsync();

            return Task.CompletedTask;
        };

        _interactions.InteractionExecuted += DI.GetInteractionsHandler.HandleInteraction;
        _discordClient.SlashCommandExecuted += DI.GetSlashCommandsHandler.HandleSlashCommand;
        _discordClient.ModalSubmitted += DI.GetModalsHandler.HandleModal;
        _discordClient.ButtonExecuted += DI.GetButtonsHandler.HandleButton;
        _discordClient.MessageReceived += DI.GetMessagesHandler.HandleMessage;

        _discordClient.Connected += OnConnected;
    }


    private async Task OnConnected()
    {
        foreach (var guild in _discordClient.Guilds)
        {
            await guild.EnsureExistInDbAsync();
        }

        var failedGuilds = new ConcurrentBag<(IGuild guild, string err)>();
        var guildChunks = _discordClient.Guilds.Chunk(20); // to not hit rate limit

        var sw = Stopwatch.StartNew();

        foreach (var guilds in guildChunks)
        {
            while (sw.Elapsed.Seconds < 1)
            {
                // wait
            }
            sw.Restart();

            await Parallel.ForEachAsync(guilds, async (guild, _) =>
            {
                try
                {
                    var commands = await guild.GetApplicationCommandsAsync();
                    if (commands.Count == 0)
                    {
                        var startCommand = ExplicitCommandBuilders.BuildStartCommand();
                        await guild.CreateApplicationCommandAsync(startCommand);
                        return;
                    }

                    var hasStartCommand = commands.Select(c => c.Name).Contains($"{ExplicitCommands.start:G}");
                    if (!hasStartCommand)
                    {
                        await _interactions.RegisterCommandsToGuildAsync(guild.Id, false);
                    }
                }
                catch (Exception e)
                {
                    failedGuilds.Add((guild, e.Message));
                    _log.Error(e.ToString());
                }
            });
        }

        sw.Stop();

        if (!failedGuilds.IsEmpty)
        {
            var guildsListLine = string.Join('\n', failedGuilds.Select(pair => $"> {pair.guild.Name} ({pair.guild.Id}): {pair.err}"));
            await _discordClient.ReportLogAsync($"Failed to register slash commands to guilds ({guildsListLine.Length})", guildsListLine, color: Color.Red);
        }

        if (BotConfig.PLAYING_STATUS.Length != 0)
        {
            await _discordClient.SetGameAsync(BotConfig.PLAYING_STATUS);
            _log.Info($"Playing status - {BotConfig.PLAYING_STATUS}");
        }

        await _discordClient.ReportLogAsync($"{_discordClient.CurrentUser.Username} - Online", null);
        BackgroundWorker.Run();
    }
}
