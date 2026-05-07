using CharacterEngineDiscord.Contracts.Requests;
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
/// </summary>
internal sealed class CeSlashCommandRouter : ICeRequestHandler<SlashCommandInvokedRequest>
{
    private readonly PingSlashCommandHandler _ping;
    private readonly ILogger<CeSlashCommandRouter> _logger;

    public CeSlashCommandRouter(
        PingSlashCommandHandler ping,
        ILogger<CeSlashCommandRouter> logger)
    {
        _ping = ping;
        _logger = logger;
    }

    public async Task HandleAsync(SlashCommandInvokedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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
}
