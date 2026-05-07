using CharacterEngineDiscord.Contracts.Commands;
using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Core.Exceptions;
using CharacterEngineDiscord.Messaging.Abstractions;
using CharacterEngineDiscord.Messaging.Handlers;
using CharacterEngineDiscord.Server.RequestHandlers;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.Server.Routing;

/// <summary>
/// Single <see cref="ICeRequestHandler{TRequest}"/> implementation for
/// <see cref="SlashCommandInvokedRequest"/>. Branches on
/// <see cref="SlashCommandInvokedRequest.CommandName"/> to dispatch to the
/// per-command handler. Unknown commands log a warning and ack — they MUST NOT
/// throw, otherwise the request consumer would requeue indefinitely.
///
/// Wraps dispatch in a try/catch for <see cref="UserFriendlyException"/>: when
/// a per-command handler raises that exception (an expected business error such
/// as "you don't have permission" or "guild not configured"), the router publishes
/// an ephemeral <see cref="RespondToInteractionCommand"/> with the exception's
/// message as the user-visible content and returns normally so the underlying
/// queue message is acked instead of requeued. Bug-class exceptions (anything
/// that is NOT <see cref="UserFriendlyException"/>) propagate to the consumer
/// for the standard requeue path.
/// </summary>
internal sealed class CeSlashCommandRouter : ICeRequestHandler<SlashCommandInvokedRequest>
{
    private readonly PingSlashCommandHandler _ping;
    private readonly ICeMessagePublisher _publisher;
    private readonly ILogger<CeSlashCommandRouter> _logger;

    public CeSlashCommandRouter(
        PingSlashCommandHandler ping,
        ICeMessagePublisher publisher,
        ILogger<CeSlashCommandRouter> logger)
    {
        _ping = ping;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(SlashCommandInvokedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            switch (request.CommandName)
            {
                case "ping":
                    await _ping.HandleAsync(request, cancellationToken);
                    break;
                default:
                    _logger.LogWarning(
                        "[{Trace}] Unknown slash command '{Command}'; ignoring",
                        request.TraceId, request.CommandName);
                    break;
            }
        }
        catch (UserFriendlyException ufx)
        {
            _logger.LogInformation(
                "[{Trace}] User-friendly error for /{Command}: {Message}",
                request.TraceId, request.CommandName, ufx.Message);

            var response = new RespondToInteractionCommand
            {
                TraceId = request.TraceId,
                MessageId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                ApplicationId = request.ApplicationId,
                InteractionToken = request.InteractionToken,
                Content = ufx.Message,
                IsEphemeral = true,
                OriginGuildId = request.GuildId,
                OriginChannelId = request.ChannelId,
            };

            await _publisher.PublishCommandAsync(response, cancellationToken);
        }
    }
}
