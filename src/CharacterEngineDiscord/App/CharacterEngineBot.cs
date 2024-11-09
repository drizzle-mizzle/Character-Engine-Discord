using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Handlers;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngine.App.Static;
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
    private readonly DiscordSocketClient _discordSocketClient;
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

        BackgroundWorker.Run();

        // Prevent application from closing
        await Task.Delay(-1);
    }

    private CharacterEngineBot(DiscordSocketClient discordSocketClient)
    {
        _discordSocketClient = discordSocketClient;
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
            await _discordSocketClient.ReportErrorAsync("UnhandledException", $"Sender: {sender}\n{e.ExceptionObject}", "?", false);
        };

        TaskScheduler.UnobservedTaskException += async (sender, e) =>
        {
            if (e.Exception.InnerException is UserFriendlyException)
            {
                return;
            }

            await _discordSocketClient.ReportErrorAsync("UnobservedTaskException", $"Sender: {sender}\n{e.Exception}", "?", false);
        };

        _discordSocketClient.JoinedGuild += guild =>
        {
            Task.Run(async () =>
            {
                var ownerName = await _discordSocketClient.GetUserAsync(guild.OwnerId) is not IUser guildOwner ? guild.OwnerId.ToString()
                        : $"{guildOwner.GlobalName ?? guildOwner.Username} ({guild.OwnerId})";

                var message = $"Joined server **{guild.Name}** ({guild.Id})\n" +
                              $"Owner: **{ownerName}**\n" +
                              $"Members: {guild.MemberCount}\n" +
                              $"Description: {guild.Description ?? "none"}";

                await _discordSocketClient.ReportLogAsync(message, color: Color.Gold, imageUrl: guild.IconUrl);
                await guild.EnsureExistInDbAsync();
                await EnsureCommandsRegisteredAsync(guild);
            });

            return Task.CompletedTask;
        };

        _discordSocketClient.LeftGuild += guild =>
        {
            Task.Run(async () =>
            {
                var ownerName = await _discordSocketClient.GetUserAsync(guild.OwnerId) is not IUser guildOwner ? guild.OwnerId.ToString()
                        : $"{guildOwner.GlobalName ?? guildOwner.Username} ({guild.OwnerId})";

                var message = $"Left server **{guild.Name}** ({guild.Id})\n" +
                              $"Owner: **{ownerName}**\n" +
                              $"Members: {guild.MemberCount}";

                await _discordSocketClient.ReportLogAsync(message, color: Color.DarkGrey, imageUrl: guild.IconUrl);
                await guild.MarkAsLeftAsync();
            });

            return Task.CompletedTask;
        };

        _discordSocketClient.ButtonExecuted += _buttonsHandler.HandleButton;
        _interactionService.InteractionExecuted += _interactionsHandler.HandleInteraction;
        _discordSocketClient.MessageReceived += _messagesHandler.HandleMessage;
        _discordSocketClient.ModalSubmitted += _modalsHandler.HandleModal;
        _discordSocketClient.SlashCommandExecuted += _slashCommandsHandler.HandleSlashCommand;

        Task.Run(async () => await RegisterCommandsAsync());

        return Task.CompletedTask;
    }


    private async Task RegisterCommandsAsync()
    {
        try
        {
            foreach (var guild in _discordSocketClient.Guilds)
            {
                await guild.EnsureExistInDbAsync();
            }

            if (_update == false)
            {
                return;
            }

            _log.Info($"[{_discordSocketClient.ShardId}] Registering commands to {_discordSocketClient.Guilds.Count} guilds");

            await ProcessAdminGuildAsync();

            var failedGuilds = new ConcurrentBag<(IGuild guild, string err)>();
            var guildChunks = _discordSocketClient.Guilds.Where(g => g.Id != BotConfig.ADMIN_GUILD_ID).Chunk(5); // to not hit rate limit

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
                await _discordSocketClient.ReportLogAsync($"Failed to register slash commands to guilds ({failedGuilds.Count})", guildsListLine, color: Color.Magenta);
            }

            _log.Info($"[{_discordSocketClient.ShardId}] Finished registering commands");
        }
        catch (Exception e)
        {
            await _discordSocketClient.ReportErrorAsync("UnhandledException", e.ToString() ?? "", _discordSocketClient.ShardId.ToString());
        }
    }


    private async Task ProcessAdminGuildAsync()
    {
        var adminGuild = _discordSocketClient.Guilds.FirstOrDefault(g => g.Id == BotConfig.ADMIN_GUILD_ID);
        if (adminGuild is null)
        {
            return;
        }

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
        var commandNames = commands.Select(c => c.Name).ToArray();

        var hasStartCommand = commandNames.Contains($"{SpecialCommands.start:G}");
        if (hasStartCommand)
        {
            return;
        }

        var hasDisableCommand = commandNames.Contains($"{SpecialCommands.disable:G}");
        if (hasDisableCommand)
        {
            await _interactionService.RegisterCommandsToGuildAsync(guild.Id);
            return;
        }

        var startCommand = ExplicitCommandBuilders.BuildStartCommand();
        await guild.CreateApplicationCommandAsync(startCommand);
    }
}
