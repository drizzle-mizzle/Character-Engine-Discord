using System.Collections.Concurrent;
using System.Reflection;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Common;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using DI = CharacterEngine.App.Helpers.Common.DependencyInjectionHelper;


namespace CharacterEngine.App;


public static class CharacterEngineBot
{
    private static readonly IServiceProvider _serviceProvider = DI.GetServiceProvider;
    private static readonly ILogger _log = _serviceProvider.GetRequiredService<ILogger>();
    private static readonly DiscordSocketClient _discordClient = _serviceProvider.GetRequiredService<DiscordSocketClient>();
    private static readonly InteractionService _interactions = _serviceProvider.GetRequiredService<InteractionService>();


    public static void Run() => RunAsync().GetAwaiter().GetResult();
    private static async Task RunAsync()
    {
        InteractionsHelper.Initialize(_serviceProvider);

        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider).ConfigureAwait(false);
        _discordClient.BindEvents();

        await _discordClient.LoginAsync(TokenType.Bot, BotConfig.BOT_TOKEN).ConfigureAwait(false);
        await _discordClient.StartAsync().ConfigureAwait(false);

        var db = _serviceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        // await db.Database.MigrateAsync();

        await Task.Delay(-1);
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

        discordClient.JoinedGuild += async guild =>
        {
            _log.Info($"Joined guild {guild.Name} ({guild.Id}) | Members: {guild.MemberCount} | Description: {guild.Description ?? "none"}");
            await Task.Run(async () => await guild.EnsureExistInDbAsync());
        };

        discordClient.LeftGuild += async guild =>
        {
            _log.Info($"Left guild {guild.Name} ({guild.Id}) | Members: {guild.MemberCount} | Description: {guild.Description ?? "none"}");
            await Task.Run(async () => await guild.MarkAsLeftAsync());
        };

        discordClient.Connected += OnConnected;

        var interactionService = _serviceProvider.GetRequiredService<InteractionService>();
        interactionService.InteractionExecuted += DI.GetInteractionsHandler.HandleInteraction;
        discordClient.SlashCommandExecuted += DI.GetSlashCommandsHandler.HandleSlashCommand;
        discordClient.ModalSubmitted += DI.GetModalsHandler.HandleModal;
        discordClient.ButtonExecuted += DI.GetButtonsHandler.HandleButton;
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
        BackgroundWorker.Run(_serviceProvider);
    }
}
