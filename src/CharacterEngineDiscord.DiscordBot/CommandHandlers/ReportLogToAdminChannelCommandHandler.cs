using CharacterEngineDiscord.Contracts.Commands;
using CharacterEngineDiscord.Messaging.Handlers;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.DiscordBot.CommandHandlers;

/// <summary>
/// Sole executor of admin-channel notifications. Receives the already-resolved
/// channel id and pre-formatted content; performs <c>SendMessageAsync</c> via the
/// cached gateway state. Throwing on transient failures (gateway not ready / Discord
/// I/O error) requeues the message so the operation is retried after reconnect.
/// </summary>
internal sealed class ReportLogToAdminChannelCommandHandler : ICeCommandHandler<ReportLogToAdminChannelCommand>
{
    private readonly DiscordShardedClient _client;
    private readonly ILogger<ReportLogToAdminChannelCommandHandler> _logger;

    public ReportLogToAdminChannelCommandHandler(
        DiscordShardedClient client,
        ILogger<ReportLogToAdminChannelCommandHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task HandleAsync(ReportLogToAdminChannelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_client.LoginState != LoginState.LoggedIn)
        {
            _logger.LogDebug(
                "[{Trace}] Skipped admin-channel report: client not logged in",
                command.TraceId);

            // throw -> nack-with-requeue: on reconnect the handler will succeed
            throw new InvalidOperationException("Discord client not logged in yet");
        }

        if (_client.GetChannel(command.TargetChannelId) is not IMessageChannel channel)
        {
            // ack-and-discard — channel not in cache; retrying does not help
            _logger.LogWarning(
                "[{Trace}] Admin channel {ChannelId} not in cache; dropping report",
                command.TraceId, command.TargetChannelId);
            return;
        }

        try
        {
            await channel.SendMessageAsync(command.Content);
            _logger.LogDebug(
                "[{Trace}] Admin-channel report delivered to {ChannelId} (isError={IsError})",
                command.TraceId, command.TargetChannelId, command.IsError);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{Trace}] SendMessageAsync failed to channel {ChannelId}",
                command.TraceId, command.TargetChannelId);
            throw; // requeue
        }
    }
}
