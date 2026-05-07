using CharacterEngineDiscord.Core.Configuration;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.DiscordBot.Hosting;

/// <summary>
/// Subscribes to <see cref="DiscordShardedClient.ShardReady"/> and, on the first ready
/// signal that includes the admin guild in cache, performs a guild-scoped bulk overwrite
/// of the application command set. Phase 2 registers a single <c>/ping</c> command.
/// Failures reset the gate so the next <see cref="DiscordSocketClient.ShardReady"/>
/// retries automatically (e.g. when the admin guild was not yet ready).
/// </summary>
internal sealed class CeSlashCommandRegistrarHostedService : IHostedService
{
    private readonly DiscordShardedClient _client;
    private readonly AdminOptions _admin;
    private readonly ILogger<CeSlashCommandRegistrarHostedService> _logger;
    private int _registered;

    public CeSlashCommandRegistrarHostedService(
        DiscordShardedClient client,
        IOptions<AdminOptions> adminOptions,
        ILogger<CeSlashCommandRegistrarHostedService> logger)
    {
        _client = client;
        _admin = adminOptions.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _client.ShardReady += OnShardReadyAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.ShardReady -= OnShardReadyAsync;
        return Task.CompletedTask;
    }

    private async Task OnShardReadyAsync(DiscordSocketClient shard)
    {
        if (Interlocked.CompareExchange(ref _registered, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var guild = _client.GetGuild(_admin.GuildId);
            if (guild is null)
            {
                _logger.LogWarning(
                    "Admin guild {GuildId} not in cache yet; will retry on next ShardReady (shard {ShardId})",
                    _admin.GuildId, shard.ShardId);
                Interlocked.Exchange(ref _registered, 0);
                return;
            }

            var ping = new SlashCommandBuilder()
                .WithName("ping")
                .WithDescription("Health-check.")
                .Build();

            await guild.BulkOverwriteApplicationCommandAsync([ping]);

            _logger.LogInformation(
                "Registered 1 application command on admin guild {GuildId} (shard {ShardId})",
                _admin.GuildId, shard.ShardId);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _registered, 0);
            _logger.LogError(ex, "Failed to register slash commands on admin guild {GuildId}", _admin.GuildId);
        }
    }
}
