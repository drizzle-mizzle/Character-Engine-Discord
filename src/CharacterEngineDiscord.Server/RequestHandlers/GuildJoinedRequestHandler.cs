using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Core.Abstractions.Logging;
using CharacterEngineDiscord.DataAccess;
using CharacterEngineDiscord.DataAccess.Models.Discord;
using CharacterEngineDiscord.Messaging.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.Server.RequestHandlers;

/// <summary>
/// Persists <see cref="GuildJoinedRequest"/> messages: insert / refresh-already-joined /
/// resurrect a soft-deleted row, then best-effort emit an admin-channel notification
/// via <see cref="IDiscordLogger"/> (which itself goes through the command bus).
/// </summary>
internal sealed class GuildJoinedRequestHandler : ICeRequestHandler<GuildJoinedRequest>
{
    private readonly AppDbContext _db;
    private readonly IDiscordLogger _discordLogger;
    private readonly ILogger<GuildJoinedRequestHandler> _logger;

    public GuildJoinedRequestHandler(
        AppDbContext db,
        IDiscordLogger discordLogger,
        ILogger<GuildJoinedRequestHandler> logger)
    {
        _db = db;
        _discordLogger = discordLogger;
        _logger = logger;
    }

    public async Task HandleAsync(GuildJoinedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _db.DiscordGuilds
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == request.GuildId, cancellationToken);

        var now = DateTime.Now;

        if (existing is null)
        {
            _db.DiscordGuilds.Add(new DiscordGuild
            {
                Id = request.GuildId,
                Name = request.Name,
                OwnerId = request.OwnerId,
                OwnerUsername = request.OwnerUsername,
                MemberCount = request.MemberCount,
                IconUrl = request.IconUrl,
                Joined = true,
                JoinedAt = now,
                LeftAt = null,
                UpdatedAt = now,
                // CreatedAt: defaulted by the database to now()
            });
        }
        else if (existing.Joined)
        {
            // refresh metadata
            existing.Name = request.Name;
            existing.OwnerId = request.OwnerId;
            existing.OwnerUsername = request.OwnerUsername;
            existing.MemberCount = request.MemberCount;
            existing.IconUrl = request.IconUrl;
            existing.UpdatedAt = now;
        }
        else
        {
            // resurrect previously-left guild
            existing.Name = request.Name;
            existing.OwnerId = request.OwnerId;
            existing.OwnerUsername = request.OwnerUsername;
            existing.MemberCount = request.MemberCount;
            existing.IconUrl = request.IconUrl;
            existing.Joined = true;
            existing.JoinedAt = now;
            existing.LeftAt = null;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[{Trace}] Joined guild persisted: {Name} ({GuildId})",
            request.TraceId, request.Name, request.GuildId);

        await _discordLogger.ReportAsync(
            new DiscordLogEntry
            {
                Title = $"Joined server: {request.Name}",
                Message = $"Owner: {request.OwnerUsername ?? request.OwnerId.ToString(System.Globalization.CultureInfo.InvariantCulture)}\nMembers: {request.MemberCount}",
                TraceId = request.TraceId,
            },
            LogLevel.Information,
            cancellationToken);
    }
}
