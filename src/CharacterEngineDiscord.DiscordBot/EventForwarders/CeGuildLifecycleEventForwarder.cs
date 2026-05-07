using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Core.Helpers;
using CharacterEngineDiscord.Messaging.Abstractions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.DiscordBot.EventForwarders;

/// <summary>
/// Translates Discord <see cref="DiscordShardedClient.JoinedGuild"/> /
/// <see cref="DiscordShardedClient.LeftGuild"/> gateway events into
/// <see cref="GuildJoinedRequest"/> / <see cref="GuildLeftRequest"/> messages on
/// the request bus. The .Server side persists them and (best-effort) emits an
/// admin-channel notification via the command bus.
/// All exceptions are caught and surfaced through <see cref="ILogger{T}"/> —
/// gateway events MUST NOT bubble up and crash the shard.
/// </summary>
internal sealed class CeGuildLifecycleEventForwarder
{
    private readonly ICeMessagePublisher _publisher;
    private readonly ILogger<CeGuildLifecycleEventForwarder> _logger;

    public CeGuildLifecycleEventForwarder(
        ICeMessagePublisher publisher,
        ILogger<CeGuildLifecycleEventForwarder> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task OnJoinedGuildAsync(SocketGuild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);

        var traceId = TraceId.New();
        try
        {
            string? ownerUsername = null;
            try
            {
                ownerUsername = guild.Owner?.Username;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[{Trace}] Failed to fetch owner username for guild {GuildId}", traceId, guild.Id);
            }

            var request = new GuildJoinedRequest
            {
                TraceId = traceId,
                MessageId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                GuildId = guild.Id,
                Name = guild.Name,
                OwnerId = guild.OwnerId,
                OwnerUsername = ownerUsername,
                MemberCount = guild.MemberCount,
                IconUrl = guild.IconUrl,
            };

            await _publisher.PublishRequestAsync(request);

            _logger.LogInformation(
                "[{Trace}] Published GuildJoinedRequest for {Name} ({GuildId})",
                traceId, guild.Name, guild.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Trace}] OnJoinedGuild forwarding failed for guild {GuildId}", traceId, guild.Id);
        }
    }

    public async Task OnLeftGuildAsync(SocketGuild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);

        var traceId = TraceId.New();
        try
        {
            var request = new GuildLeftRequest
            {
                TraceId = traceId,
                MessageId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                GuildId = guild.Id,
                Name = guild.Name,
            };

            await _publisher.PublishRequestAsync(request);

            _logger.LogInformation(
                "[{Trace}] Published GuildLeftRequest for {Name} ({GuildId})",
                traceId, guild.Name, guild.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Trace}] OnLeftGuild forwarding failed for guild {GuildId}", traceId, guild.Id);
        }
    }
}
