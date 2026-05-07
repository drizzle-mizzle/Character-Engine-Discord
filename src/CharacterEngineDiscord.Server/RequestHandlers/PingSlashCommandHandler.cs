using CharacterEngineDiscord.Contracts.Commands;
using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Messaging.Abstractions;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.Server.RequestHandlers;

/// <summary>
/// Handles the <c>/ping</c> slash command. Produces a single
/// <see cref="RespondToInteractionCommand"/> containing a user mention + "Pong!" and
/// publishes it on the command bus for the Bot to deliver as an interaction followup.
/// Not <c>sealed</c> and <see cref="HandleAsync"/> is <c>virtual</c> solely to allow
/// router-level tests to substitute the dispatch behavior with a stubbed override.
/// </summary>
internal class PingSlashCommandHandler
{
    private readonly ICeMessagePublisher _publisher;
    private readonly ILogger<PingSlashCommandHandler> _logger;

    public PingSlashCommandHandler(
        ICeMessagePublisher publisher,
        ILogger<PingSlashCommandHandler> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public virtual async Task HandleAsync(SlashCommandInvokedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = $"<@{request.UserId}> Pong!";

        var command = new RespondToInteractionCommand
        {
            TraceId = request.TraceId,
            MessageId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            ApplicationId = request.ApplicationId,
            InteractionToken = request.InteractionToken,
            Content = content,
            IsEphemeral = false,
            OriginGuildId = request.GuildId,
            OriginChannelId = request.ChannelId,
        };

        await _publisher.PublishCommandAsync(command, cancellationToken);

        _logger.LogInformation(
            "[{Trace}] /ping handled for user {UserId} in guild {GuildId}",
            request.TraceId, request.UserId, request.GuildId);
    }
}
