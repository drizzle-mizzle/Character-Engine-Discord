using CharacterEngineDiscord.Core.Abstractions.Logging;
using CharacterEngineDiscord.Core.Configuration;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace CharacterEngineDiscord.Hosting;

/// <summary>
/// Drives the lifecycle of the singleton <see cref="DiscordShardedClient"/>:
/// wires gateway-event subscriptions, performs login + start during the host's
/// <see cref="StartAsync"/> phase (fail-fast — startup exceptions abort the host),
/// then unsubscribes and best-effort cleans up on shutdown.
/// </summary>
internal sealed class CeDiscordBotHostedService : IHostedService
{
    private readonly DiscordShardedClient _client;
    private readonly BotOptions _botOptions;
    private readonly IDiscordLogger _discordLogger;
    private readonly GuildLifecycleHandler _guildHandler;
    private readonly ILogger<CeDiscordBotHostedService> _logger;

    public CeDiscordBotHostedService(
        DiscordShardedClient client,
        IOptions<BotOptions> botOptions,
        IDiscordLogger discordLogger,
        GuildLifecycleHandler guildHandler,
        ILogger<CeDiscordBotHostedService> logger)
    {
        _client = client;
        _botOptions = botOptions.Value;
        _discordLogger = discordLogger;
        _guildHandler = guildHandler;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += OnDiscordLogAsync;
        _client.JoinedGuild += _guildHandler.OnJoinedGuildAsync;
        _client.LeftGuild += _guildHandler.OnLeftGuildAsync;
        _client.ShardReady += OnShardReadyAsync;

        await _client.LoginAsync(TokenType.Bot, _botOptions.Token, validateToken: true);
        await _client.StartAsync();

        if (!string.IsNullOrWhiteSpace(_botOptions.PlayingStatus))
        {
            await _client.SetGameAsync(_botOptions.PlayingStatus);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _client.Log -= OnDiscordLogAsync;
        _client.JoinedGuild -= _guildHandler.OnJoinedGuildAsync;
        _client.LeftGuild -= _guildHandler.OnLeftGuildAsync;
        _client.ShardReady -= OnShardReadyAsync;

        try
        {
            await _client.SetGameAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to clear status on shutdown");
        }

        try
        {
            await _client.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to stop Discord client on shutdown");
        }

        try
        {
            await _client.LogoutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to log out Discord client on shutdown");
        }
    }

    private Task OnDiscordLogAsync(LogMessage msg)
    {
        var level = msg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error    => LogLevel.Error,
            LogSeverity.Warning  => LogLevel.Warning,
            LogSeverity.Info     => LogLevel.Information,
            LogSeverity.Verbose  => LogLevel.Debug,
            LogSeverity.Debug    => LogLevel.Trace,
        };

        if (msg.Exception is not null)
        {
            _logger.Log(level, msg.Exception, "[{Source}] {Message}", msg.Source, msg.Message ?? string.Empty);
        }
        else
        {
            _logger.Log(level, "[{Source}] {Message}", msg.Source, msg.Message ?? string.Empty);
        }

        return Task.CompletedTask;
    }

    private async Task OnShardReadyAsync(DiscordSocketClient shard)
    {
        _logger.LogInformation("Shard ready: ShardId={ShardId}, Latency={Latency}ms", shard.ShardId, shard.Latency);

        await _discordLogger.ReportAsync(
            new DiscordLogEntry { Title = $"Shard {shard.ShardId} ready" },
            LogLevel.Information);
    }
}
