using System.Globalization;
using System.Text;
using CharacterEngineDiscord.Contracts.Commands;
using CharacterEngineDiscord.Core.Abstractions.Logging;
using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.Core.Helpers;
using CharacterEngineDiscord.Messaging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Server.Logging;

/// <summary>
/// Server-side <see cref="IDiscordLogger"/>. Resolves the target admin channel
/// from <see cref="AdminOptions"/> (logs vs errors) according to severity, then
/// publishes a <see cref="ReportLogToAdminChannelCommand"/> that the Bot's
/// <c>ReportLogToAdminChannelCommandHandler</c> ultimately delivers.
/// Implementation is intentionally duplicated with the Bot-side variant because
/// the alternatives — pulling <see cref="ICeMessagePublisher"/> into Core, or
/// hosting <see cref="IDiscordLogger"/> in Messaging — both worsen layering.
/// </summary>
internal sealed class CeDiscordLogger : IDiscordLogger
{
    private const int MaxDiscordMessageLength = 1990;

    private readonly ICeMessagePublisher _publisher;
    private readonly AdminOptions _adminOptions;
    private readonly ILogger<CeDiscordLogger> _logger;

    public CeDiscordLogger(
        ICeMessagePublisher publisher,
        IOptions<AdminOptions> adminOptions,
        ILogger<CeDiscordLogger> logger)
    {
        _publisher = publisher;
        _adminOptions = adminOptions.Value;
        _logger = logger;
    }

    public async Task ReportAsync(DiscordLogEntry entry, LogLevel severity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var isError = severity >= LogLevel.Error;
        var targetId = isError ? _adminOptions.ErrorsChannelId : _adminOptions.LogsChannelId;
        if (targetId == 0)
        {
            return;
        }

        var content = FormatContent(entry, isError);

        var command = new ReportLogToAdminChannelCommand
        {
            TraceId = entry.TraceId ?? TraceId.New(),
            MessageId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            TargetChannelId = targetId,
            Content = content,
            IsError = isError,
        };

        try
        {
            await _publisher.PublishCommandAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish ReportLogToAdminChannelCommand");
        }
    }

    private static string FormatContent(DiscordLogEntry entry, bool isError)
    {
        var prefix = isError ? "[ERROR]" : "[INFO]";
        var traceLine = entry.TraceId is null ? string.Empty : $" [{entry.TraceId}]";

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{prefix}{traceLine} **{entry.Title}**");
        if (!string.IsNullOrEmpty(entry.Message))
        {
            sb.Append('\n').Append(entry.Message);
        }
        if (entry.Exception is not null)
        {
            sb.Append('\n').Append("```\n").Append(entry.Exception).Append("\n```");
        }

        var content = sb.ToString();
        if (content.Length > MaxDiscordMessageLength)
        {
            content = string.Concat(content.AsSpan(0, MaxDiscordMessageLength), "[...]");
        }
        return content;
    }
}
