using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Core.Abstractions.Logging;
using CharacterEngineDiscord.DataAccess;
using CharacterEngineDiscord.Messaging.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.Server.RequestHandlers;

/// <summary>
/// Persists <see cref="GuildLeftRequest"/> messages: marks the matching row
/// soft-deleted (<c>Joined=false</c>, <c>LeftAt=now</c>), then best-effort emits an
/// admin-channel notification via <see cref="IDiscordLogger"/>.
/// Idempotent: unknown or already-left rows are logged and skipped without throwing.
/// </summary>
internal sealed class GuildLeftRequestHandler : ICeRequestHandler<GuildLeftRequest>
{
    private readonly AppDbContext _db;
    private readonly IDiscordLogger _discordLogger;
    private readonly ILogger<GuildLeftRequestHandler> _logger;

    public GuildLeftRequestHandler(
        AppDbContext db,
        IDiscordLogger discordLogger,
        ILogger<GuildLeftRequestHandler> logger)
    {
        _db = db;
        _discordLogger = discordLogger;
        _logger = logger;
    }

    public async Task HandleAsync(GuildLeftRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _db.DiscordGuilds
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == request.GuildId, cancellationToken);

        if (existing is null)
        {
            _logger.LogWarning(
                "[{Trace}] LeftGuild for unknown guild {GuildId}",
                request.TraceId, request.GuildId);
            return;
        }

        if (!existing.Joined)
        {
            _logger.LogWarning(
                "[{Trace}] LeftGuild for already-left guild {Name} ({GuildId}); idempotent skip",
                request.TraceId, request.Name, request.GuildId);
            return;
        }

        var now = DateTime.Now;
        existing.Joined = false;
        existing.LeftAt = now;
        existing.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[{Trace}] Left guild persisted: {Name} ({GuildId})",
            request.TraceId, request.Name, request.GuildId);

        await _discordLogger.ReportAsync(
            new DiscordLogEntry
            {
                Title = $"Left server: {request.Name}",
                Message = $"Id: {request.GuildId}",
                TraceId = request.TraceId,
            },
            LogLevel.Information,
            cancellationToken);
    }
}
