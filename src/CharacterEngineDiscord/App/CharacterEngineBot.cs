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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using DI = CharacterEngine.App.Helpers.Infrastructure.DependencyInjectionHelper;

namespace CharacterEngine.App;


public sealed class CharacterEngineBot
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly Dictionary<int, CharacterEngineBot> _instatnces = []; // int = ShardId

    private readonly DiscordSocketClient _discordClient;
    private readonly ServiceProvider _serviceProvider;
    private readonly InteractionService _interactionService;

    public static DiscordShardedClient DiscordClient { get; private set; } = null!;


    private CharacterEngineBot(DiscordSocketClient discordSocketClient)
    {
        _discordClient = discordSocketClient;
        _interactionService = new InteractionService(discordSocketClient.Rest);

        _serviceProvider = DI.BuildServiceProvider(discordSocketClient, _interactionService);
        _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider).Wait();

        Congifure();
    }


    private void Congifure()
    {
        _discordClient.JoinedGuild += OnJoinedGuild;
        _discordClient.LeftGuild += OnLeftGuild;
        _discordClient.ButtonExecuted += _serviceProvider.GetRequiredService<ButtonsHandler>().HandleButton;
        _interactionService.InteractionExecuted += _serviceProvider.GetRequiredService<InteractionsHandler>().HandleInteraction;
        _discordClient.MessageReceived += _serviceProvider.GetRequiredService<MessagesHandler>().HandleMessage;
        _discordClient.ModalSubmitted += _serviceProvider.GetRequiredService<ModalsHandler>().HandleModal;
        _discordClient.SlashCommandExecuted += _serviceProvider.GetRequiredService<SlashCommandsHandler>().HandleSlashCommand;

        Task.Run(async () =>
        {
            // Process admin guild
            var adminGuild = _discordClient.Guilds.FirstOrDefault(g => g.Id == BotConfig.ADMIN_GUILD_ID);
            if (adminGuild is not null)
            {
                await RegisterCommandsToAdminGuildAsync(adminGuild, _interactionService);
                await _discordClient.ReportLogAsync($"[ {DiscordClient.CurrentUser.Username} - Online ]", logToConsole: true);
                BackgroundWorker.Run();
            }

            // Process all other guilds in the shard
            await RegisterCommandsToAllGuildsAsync();
        });
    }


    private Task OnJoinedGuild(SocketGuild guild)
    {
        Task.Run(async () =>
        {
            guild.EnsureCached();
            var ensureCommandsRegisteredAsync = EnsureCommandsRegisteredAsync(guild);

            MetricsWriter.Create(MetricType.JoinedGuild, guild.Id);
            var message = $"Joined server **{guild.Name}** ({guild.Id})\n" + $"Owner: {await GetGuildOwnerNameAsync(guild)}\n" + $"Members: {guild.MemberCount}\n" + $"Description: {guild.Description ?? "none"}";

            await _discordClient.ReportLogAsync(message, color: Color.Gold, imageUrl: guild.IconUrl);
            await ensureCommandsRegisteredAsync;
        });

        return Task.CompletedTask;
    }


    private Task OnLeftGuild(SocketGuild guild)
    {
        Task.Run(async () =>
        {
            MetricsWriter.Create(MetricType.LeftGuild, guild.Id);
            var message = $"Left server **{guild.Name}** ({guild.Id})\n" + $"Owner: {await GetGuildOwnerNameAsync(guild)}\n" + $"Members: {guild.MemberCount}";

            await _discordClient.ReportLogAsync(message, color: Color.DarkOrange, imageUrl: guild.IconUrl);
            await guild.MarkAsLeftAsync();
        });

        return Task.CompletedTask;
    }


    private async Task<string> GetGuildOwnerNameAsync(IGuild g)
        => await _discordClient.GetUserAsync(g.OwnerId) is IUser o ? $"**{o.Username}** ({g.OwnerId})" : g.OwnerId.ToString();


    public static async Task RunAsync()
    {
        var cacheTask = CacheUsersAndCharacters();

        DiscordClient = new DiscordShardedClient(new DiscordSocketConfig
        {
            MessageCacheSize = 10,
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.MessageContent,
            ConnectionTimeout = 30_000,
            DefaultRetryMode = RetryMode.RetryRatelimit,
            AlwaysDownloadDefaultStickers = true,
            MaxWaitBetweenGuildAvailablesBeforeReady = (int)TimeSpan.FromMinutes(5).TotalMilliseconds,
        });

        DiscordClient.Log += OnShardLog;
        AppDomain.CurrentDomain.UnhandledException += HandleExceptionAsync;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

        DiscordClient.ShardReady += (discordSocketClient) =>
        {
            if (!_instatnces.ContainsKey(discordSocketClient.ShardId))
            {
                _instatnces.Add(discordSocketClient.ShardId, new CharacterEngineBot(discordSocketClient));
            }

            return Task.CompletedTask;
        };

        await DiscordClient.LoginAsync(TokenType.Bot, BotConfig.BOT_TOKEN);

        await cacheTask; // Don't start until caching complete

        await DiscordClient.StartAsync();

        if (BotConfig.PLAYING_STATUS.Length != 0)
        {
            await DiscordClient.SetGameAsync(BotConfig.PLAYING_STATUS);
            _log.Info($"[ Playing status - {BotConfig.PLAYING_STATUS} ]");
        }

        // Prevent application from closing
        await Task.Delay(-1);
    }


    private static Task CacheUsersAndCharacters()
    {
        var users = Task.Run(async () =>
        {
            await using var db = DatabaseHelper.GetDbContext();
            var allCachedUserIds = await db.DiscordUsers.Select(u => u.Id).ToArrayAsync();

            Parallel.ForEach(allCachedUserIds, (userId, _) =>
            {
                MemoryStorage.CachedUsers.TryAdd(userId, null);
            });

            _log.Info($"Cached {allCachedUserIds.Length} users");
        });

        var characters = Task.Run(async () =>
        {
            var allCharacters = await DatabaseHelper.GetAllSpawnedCharactersAsync();

            await MemoryStorage.CachedCharacters.AddRangeAsync(allCharacters);
            _log.Info($"Cached {allCharacters.Count} characters");
        });

        Task.WaitAll(users, characters);
        return Task.CompletedTask;
    }


    private async Task RegisterCommandsToAllGuildsAsync()
    {
        _log.Info($"[{_discordClient.ShardId}] Registering commands to {_discordClient.Guilds.Count} guilds");

        var guildChunks = _discordClient.Guilds.Where(g => g.Id != BotConfig.ADMIN_GUILD_ID).Chunk(5); // to not hit rate limit
        var failedGuilds = new ConcurrentBag<(IGuild guild, string err)>();

        var sw = Stopwatch.StartNew();

        foreach (var guilds in guildChunks)
        {
            while (sw.Elapsed.Seconds < 1)
            {
                await Task.Delay(100);
            }

            sw.Restart();

            await Parallel.ForEachAsync(guilds, async (guild, _) =>
            {
                try
                {
                    guild.EnsureCached();
                    await EnsureCommandsRegisteredAsync(guild);
                }
                catch (Exception e)
                {
                    _log.Error(e.ToString());
                    failedGuilds.Add((guild, e.Message));
                }
            });
        }

        sw.Stop();

        if (!failedGuilds.IsEmpty)
        {
            var guildsListLine = string.Join('\n', failedGuilds.Select(pair => $"> {pair.guild.Name} ({pair.guild.Id}): {pair.err}"));
            await _discordClient.ReportLogAsync($"Failed to register slash commands to guilds ({failedGuilds.Count})", guildsListLine, color: Color.Magenta);
        }

        _log.Info($"[{_discordClient.ShardId}] Finished registering commands");
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

        if (commandNames.Contains($"{SpecialCommands.disable:G}"))
        {
            await _interactionService.RegisterCommandsToGuildAsync(guild.Id);
            await guild.CreateApplicationCommandAsync(ExplicitCommandBuilders.BuildDisableCommand());
        }
        else
        {
            await guild.CreateApplicationCommandAsync(ExplicitCommandBuilders.BuildStartCommand());
        }
    }


    private static async Task RegisterCommandsToAdminGuildAsync(SocketGuild adminGuild, InteractionService interactionService)
    {
        adminGuild.EnsureCached();
        await interactionService.RegisterCommandsToGuildAsync(adminGuild.Id);

        var disableCommand = ExplicitCommandBuilders.BuildDisableCommand();
        await adminGuild.CreateApplicationCommandAsync(disableCommand);

        var adminCommands = ExplicitCommandBuilders.BuildAdminCommands();
        foreach (var adminCommand in adminCommands)
        {
            await adminGuild.CreateApplicationCommandAsync(adminCommand);
        }

        _log.Info("[ Registered admin guild commands ]");
    }


    private static Task OnShardLog(LogMessage msg)
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
    }


    private static void HandleExceptionAsync(object sender, UnhandledExceptionEventArgs e)
        => Task.Run(async () => await DiscordClient.ReportErrorAsync("UnhandledException", null, $"Sender: {sender}\n{e.ExceptionObject}", "?", false));


    // ReSharper disable once AsyncVoidMethod
    private static async void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception.InnerException is UserFriendlyException)
        {
            return;
        }

        await DiscordClient.ReportErrorAsync("UnobservedTaskException", null, $"Sender: {sender}\n{e.Exception}", "?", false);
    }
}
