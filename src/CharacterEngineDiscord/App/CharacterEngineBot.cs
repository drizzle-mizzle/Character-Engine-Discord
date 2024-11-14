using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Handlers;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using DI = CharacterEngine.App.Helpers.Infrastructure.DependencyInjectionHelper;

namespace CharacterEngine.App;


public class CharacterEngineBot
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    public static DiscordShardedClient DiscordShardedClient { get; private set; } = null!;
    private static bool _update;

    private readonly InteractionService _interactionService;
    private readonly DiscordSocketClient _discordClient;
    private const GatewayIntents INTENTS = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.MessageContent;

    private readonly ButtonsHandler _buttonsHandler;
    private readonly InteractionsHandler _interactionsHandler;
    private readonly MessagesHandler _messagesHandler;
    private readonly ModalsHandler _modalsHandler;
    private readonly SlashCommandsHandler _slashCommandsHandler;


    public static async Task RunAsync(bool update)
    {
        _update = update;

        // Cache all characters from db
        _ = Task.Run(async () =>
        {
            var allCharacters = await DatabaseHelper.GetAllSpawnedCharactersAsync();
            MemoryStorage.CachedCharacters.AddRange(allCharacters);
            _log.Info($"Cached {allCharacters.Count} characters");
        });

        var clientConfig = new DiscordSocketConfig
        {
            MessageCacheSize = 10,
            GatewayIntents = INTENTS,
            ConnectionTimeout = 20_000,
            DefaultRetryMode = RetryMode.RetryRatelimit,
            AlwaysDownloadDefaultStickers = true,
            MaxWaitBetweenGuildAvailablesBeforeReady = (int)TimeSpan.FromMinutes(5).TotalSeconds,
        };

        DiscordShardedClient = new DiscordShardedClient(clientConfig);
        DiscordShardedClient.ShardReady += (discordSocketClient) => new CharacterEngineBot(discordSocketClient).Run();
        DiscordShardedClient.Log += msg =>
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

        // Connect
        await DiscordShardedClient.LoginAsync(TokenType.Bot, BotConfig.BOT_TOKEN);

        // Launch the bot
        await DiscordShardedClient.StartAsync();

        if (BotConfig.PLAYING_STATUS.Length != 0)
        {
            await DiscordShardedClient.SetGameAsync(BotConfig.PLAYING_STATUS);
            _log.Info($"Playing status - {BotConfig.PLAYING_STATUS}");
        }

        // Prevent application from closing
        await Task.Delay(-1);
    }

    private CharacterEngineBot(DiscordSocketClient discordSocketClient)
    {
        _discordClient = discordSocketClient;
        _interactionService = new InteractionService(discordSocketClient.Rest);

        var serviceProvider = DI.BuildServiceProvider(discordSocketClient, _interactionService);
        _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider).Wait();

        _buttonsHandler = serviceProvider.GetRequiredService<ButtonsHandler>();
        _interactionsHandler = serviceProvider.GetRequiredService<InteractionsHandler>();
        _messagesHandler = serviceProvider.GetRequiredService<MessagesHandler>();
        _modalsHandler = serviceProvider.GetRequiredService<ModalsHandler>();
        _slashCommandsHandler = serviceProvider.GetRequiredService<SlashCommandsHandler>();
    }


    private Task Run()
    {
        AppDomain.CurrentDomain.UnhandledException += async (sender, e) =>
        {
            await _discordClient.ReportErrorAsync("UnhandledException", $"Sender: {sender}\n{e.ExceptionObject}", "?", false);
        };

        TaskScheduler.UnobservedTaskException += async (sender, e) =>
        {
            if (e.Exception.InnerException is UserFriendlyException)
            {
                return;
            }

            await _discordClient.ReportErrorAsync("UnobservedTaskException", $"Sender: {sender}\n{e.Exception}", "?", false);
        };

        _discordClient.JoinedGuild += guild =>
        {
            Task.Run(async () =>
            {
                var ensureCommandsRegisteredAsync = EnsureCommandsRegisteredAsync(guild);
                var ensureExistInDbAsync = guild.EnsureExistInDbAsync();

                MetricsWriter.Create(MetricType.JoinedGuild, guild.Id);
                var message = $"Joined server **{guild.Name}** ({guild.Id})\n" +
                              $"Owner: {GetGuildOwnerNameAsync(guild)}\n" +
                              $"Members: {guild.MemberCount}\n" +
                              $"Description: {guild.Description ?? "none"}";

                await _discordClient.ReportLogAsync(message, color: Color.Gold, imageUrl: guild.IconUrl);
                await ensureCommandsRegisteredAsync;
                await ensureExistInDbAsync;
            });

            return Task.CompletedTask;
        };

        _discordClient.LeftGuild += guild =>
        {
            Task.Run(async () =>
            {
                MetricsWriter.Create(MetricType.LeftGuild, guild.Id);
                var message = $"Left server **{guild.Name}** ({guild.Id})\n" +
                              $"Owner: {GetGuildOwnerNameAsync(guild)}\n" +
                              $"Members: {guild.MemberCount}";

                await _discordClient.ReportLogAsync(message, color: Color.DarkGrey, imageUrl: guild.IconUrl);
                await guild.MarkAsLeftAsync();
            });

            return Task.CompletedTask;
        };

        _interactionService.InteractionExecuted += _interactionsHandler.HandleInteraction;
        _discordClient.ButtonExecuted += _buttonsHandler.HandleButton;
        _discordClient.MessageReceived += _messagesHandler.HandleMessage;
        _discordClient.ModalSubmitted += _modalsHandler.HandleModal;
        _discordClient.SlashCommandExecuted += _slashCommandsHandler.HandleSlashCommand;

        Task.Run(async () => await RegisterCommandsAsync());

        if (_discordClient.Guilds.Any(g => g.Id == BotConfig.ADMIN_GUILD_ID))
        {
            Task.Run(async () => await _discordClient.ReportLogAsync("Online"));
            BackgroundWorker.Run();
        }

        return Task.CompletedTask;
    }


    private async Task RegisterCommandsAsync()
    {
        try
        {
            foreach (var guild in _discordClient.Guilds)
            {
                await guild.EnsureExistInDbAsync();
            }

            if (_update == false)
            {
                return;
            }

            _log.Info($"[{_discordClient.ShardId}] Registering commands to {_discordClient.Guilds.Count} guilds");


            var adminGuild = _discordClient.Guilds.FirstOrDefault(g => g.Id == BotConfig.ADMIN_GUILD_ID);
            if (adminGuild is not null)
            {
                await ProcessAdminGuildAsync(adminGuild);
            }

            var failedGuilds = new ConcurrentBag<(IGuild guild, string err)>();
            var guildChunks = _discordClient.Guilds.Where(g => g.Id != BotConfig.ADMIN_GUILD_ID).Chunk(5); // to not hit rate limit

            var sw = Stopwatch.StartNew();

            foreach (var guilds in guildChunks)
            {
                while (sw.Elapsed.Seconds < 1) { } // wait
                sw.Restart();

                await Parallel.ForEachAsync(guilds, TryToProcessGuild);
                continue;

                async ValueTask TryToProcessGuild(SocketGuild guild, CancellationToken _)
                {
                    try
                    {
                        await EnsureCommandsRegisteredAsync(guild);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e.ToString());
                        failedGuilds.Add((guild, e.Message));
                    }
                }
            }

            sw.Stop();

            if (!failedGuilds.IsEmpty)
            {
                var guildsListLine = string.Join('\n', failedGuilds.Select(pair => $"> {pair.guild.Name} ({pair.guild.Id}): {pair.err}"));
                await _discordClient.ReportLogAsync($"Failed to register slash commands to guilds ({failedGuilds.Count})", guildsListLine, color: Color.Magenta);
            }

            _log.Info($"[{_discordClient.ShardId}] Finished registering commands");
        }
        catch (Exception e)
        {
            await _discordClient.ReportErrorAsync("UnhandledException", e, _discordClient.ShardId.ToString(), writeMetric: false);
        }
    }


    private async Task ProcessAdminGuildAsync(SocketGuild adminGuild)
    {
        await _interactionService.RegisterCommandsToGuildAsync(adminGuild.Id);

        var disableCommand = ExplicitCommandBuilders.BuildDisableCommand();
        await adminGuild.CreateApplicationCommandAsync(disableCommand);

        var adminCommands = ExplicitCommandBuilders.BuildAdminCommands();
        foreach (var adminCommand in adminCommands)
        {
            await adminGuild.CreateApplicationCommandAsync(adminCommand);
        }

        _log.Info("[ Registered admin guild commands ]");
    }


    private async Task EnsureCommandsRegisteredAsync(SocketGuild guild)
    {
        var commands = await guild.GetApplicationCommandsAsync();
        if (commands.Count == 0)
        {
            await guild.CreateApplicationCommandAsync(ExplicitCommandBuilders.BuildStartCommand());
            return;
        }

        var commandNames = commands.Select(c => c.Name).ToArray();
        if (commandNames.Contains($"{SpecialCommands.start:G}"))
        {
            return;
        }

        if (!commandNames.Contains($"{SpecialCommands.disable:G}"))
        {
            await guild.CreateApplicationCommandAsync(ExplicitCommandBuilders.BuildStartCommand());
            return;
        }

        await _interactionService.RegisterCommandsToGuildAsync(guild.Id);
        await guild.CreateApplicationCommandAsync(ExplicitCommandBuilders.BuildDisableCommand());
    }


    private async Task<string> GetGuildOwnerNameAsync(IGuild guild)
        => await _discordClient.GetUserAsync(guild.OwnerId) is IUser go
                ? $"**{go.GlobalName ?? go.Username}** ({guild.OwnerId})"
                : guild.OwnerId.ToString();
}
