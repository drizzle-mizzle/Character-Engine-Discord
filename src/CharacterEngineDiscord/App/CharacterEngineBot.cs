using System.Collections.Concurrent;
using System.Reflection;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Helpers.Common;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using DI = CharacterEngine.App.Helpers.Infrastructure.DependencyInjectionHelper;


namespace CharacterEngine.App;


public static class CharacterEngineBot
{
    private static DiscordSocketClient _discordClient = null!;
    private static InteractionService _interactions = null!;
    private static ILogger _log = null!;


    public static void Run() => RunAsync().GetAwaiter().GetResult();
    private static async Task RunAsync()
    {
        var serviceProvider = DI.BuildServiceProvider();
        _interactions = serviceProvider.GetRequiredService<InteractionService>();

        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider).ConfigureAwait(false);

        // Cache all characters from db
        var allCharacters = await DatabaseHelper.GetAllSpawnedCharactersAsync();
        StaticStorage.CachedCharacters.AddRange(allCharacters);

        // Configure event handlers
        await BindEventsAsync();

        // Connect
        _discordClient = DI.GetDiscordSocketClient;
        await _discordClient.LoginAsync(TokenType.Bot, BotConfig.BOT_TOKEN).ConfigureAwait(false);
        await _discordClient.StartAsync().ConfigureAwait(false);

        // Prevent application from closing
        await Task.Delay(-1);
    }


    private static async Task BindEventsAsync()
    {
        _discordClient.Connected += OnConnected;

        _log = DI.GetLogger;
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

        _discordClient.JoinedGuild += async guild =>
        {
            _log.Info($"Joined guild {guild.Name} ({guild.Id}) | Members: {guild.MemberCount} | Description: {guild.Description ?? "none"}");
            await Task.Run(async () => await guild.EnsureExistInDbAsync());
        };

        _discordClient.LeftGuild += async guild =>
        {
            _log.Info($"Left guild {guild.Name} ({guild.Id}) | Members: {guild.MemberCount} | Description: {guild.Description ?? "none"}");
            await Task.Run(async () => await guild.MarkAsLeftAsync());
        };


        _interactions.InteractionExecuted += DI.GetInteractionsHandler.HandleInteraction;
        _discordClient.SlashCommandExecuted += DI.GetSlashCommandsHandler.HandleSlashCommand;
        _discordClient.ModalSubmitted += DI.GetModalsHandler.HandleModal;
        _discordClient.ButtonExecuted += DI.GetButtonsHandler.HandleButton;
        _discordClient.MessageReceived += DI.GetMessagesHandler.HandleMessage;
    }


    private static async Task OnConnected()
    {
        foreach (var guild in _discordClient.Guilds)
        {
            await guild.EnsureExistInDbAsync();
        }

        var failedGuilds = new ConcurrentBag<(IGuild guild, string err)>();
        await Parallel.ForEachAsync(_discordClient.Guilds, async (guild, _) =>
        {
            try
            {
                var commands = await guild.GetApplicationCommandsAsync();
                if (commands.Count == 0)
                {
                    var startCommand = InteractionsHelper.BuildStartCommand();
                    await guild.CreateApplicationCommandAsync(startCommand).ConfigureAwait(false);
                    return;
                }

                if (commands.Select(c => c.Name).Contains("start"))
                {
                    return;
                }

                await _interactions.RegisterCommandsToGuildAsync(guild.Id, false);
            }
            catch (Exception e)
            {
                failedGuilds.Add((guild, e.Message));
                _log.Error(e.ToString());
            }
        });

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
