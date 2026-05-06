using CharacterEngineDiscord.Core.Abstractions.Logging;
using CharacterEngineDiscord.Core.Helpers;
using CharacterEngineDiscord.DataAccess;
using CharacterEngineDiscord.DataAccess.Models.Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.DiscordBot.Hosting;

/// <summary>
/// Bridges Discord <c>JoinedGuild</c> / <c>LeftGuild</c> gateway events into
/// <see cref="DiscordGuild"/> rows. Handler is a singleton; it spins up an
/// <see cref="IServiceScope"/> per call so the scoped <see cref="AppDbContext"/>
/// stays correctly scoped to a single unit of work.
/// All exceptions are caught and surfaced through <see cref="ILogger{T}"/> /
/// <see cref="IDiscordLogger"/> — gateway events should never bubble up and
/// crash the shard.
/// </summary>
internal sealed class GuildLifecycleHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDiscordLogger _discordLogger;
    private readonly ILogger<GuildLifecycleHandler> _logger;

    public GuildLifecycleHandler(
        IServiceScopeFactory scopeFactory,
        IDiscordLogger discordLogger,
        ILogger<GuildLifecycleHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _discordLogger = discordLogger;
        _logger = logger;
    }

    public async Task OnJoinedGuildAsync(SocketGuild guild)
    {
        var traceId = TraceId.New();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existing = await db.DiscordGuilds
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Id == guild.Id);

            string? ownerUsername = null;
            try
            {
                ownerUsername = guild.Owner?.Username;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to fetch owner username for guild {GuildId}", guild.Id);
            }

            var now = DateTime.Now;

            if (existing is null)
            {
                db.DiscordGuilds.Add(new DiscordGuild
                {
                    Id = guild.Id,
                    Name = guild.Name,
                    OwnerId = guild.OwnerId,
                    OwnerUsername = ownerUsername,
                    MemberCount = guild.MemberCount,
                    IconUrl = guild.IconUrl,
                    Joined = true,
                    JoinedAt = now,
                    LeftAt = null,
                    UpdatedAt = now,
                });
            }
            else if (existing.Joined)
            {
                existing.Name = guild.Name;
                existing.OwnerId = guild.OwnerId;
                existing.OwnerUsername = ownerUsername;
                existing.MemberCount = guild.MemberCount;
                existing.IconUrl = guild.IconUrl;
                existing.UpdatedAt = now;
            }
            else
            {
                existing.Name = guild.Name;
                existing.OwnerId = guild.OwnerId;
                existing.OwnerUsername = ownerUsername;
                existing.MemberCount = guild.MemberCount;
                existing.IconUrl = guild.IconUrl;
                existing.Joined = true;
                existing.JoinedAt = now;
                existing.LeftAt = null;
                existing.UpdatedAt = now;
            }

            await db.SaveChangesAsync();

            _logger.LogInformation("[{TraceId}] Joined guild {Name} ({GuildId})", traceId, guild.Name, guild.Id);

            await _discordLogger.ReportAsync(new DiscordLogEntry
            {
                Title = $"Joined server: {guild.Name}",
                Message = $"Owner: {ownerUsername ?? guild.OwnerId.ToString()}\nMembers: {guild.MemberCount}",
                TraceId = traceId,
            }, LogLevel.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{TraceId}] OnJoinedGuild failed for guild {GuildId}", traceId, guild.Id);
        }
    }

    public async Task OnLeftGuildAsync(SocketGuild guild)
    {
        var traceId = TraceId.New();

        try
        {
            var now = DateTime.Now;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existing = await db.DiscordGuilds
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Id == guild.Id);

            if (existing is null)
            {
                _logger.LogWarning("[{TraceId}] LeftGuild for unknown guild {GuildId}", traceId, guild.Id);
                return;
            }

            if (!existing.Joined)
            {
                _logger.LogWarning("[{TraceId}] LeftGuild for already-left guild {Name} ({GuildId})", traceId, guild.Name, guild.Id);
                return;
            }

            existing.Joined = false;
            existing.LeftAt = now;
            existing.UpdatedAt = now;

            await db.SaveChangesAsync();

            _logger.LogInformation("[{TraceId}] Left guild {Name} ({GuildId})", traceId, guild.Name, guild.Id);

            await _discordLogger.ReportAsync(new DiscordLogEntry
            {
                Title = $"Left server: {guild.Name}",
                Message = $"Id: {guild.Id}",
                TraceId = traceId,
            }, LogLevel.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{TraceId}] OnLeftGuild failed for guild {GuildId}", traceId, guild.Id);
        }
    }
}
