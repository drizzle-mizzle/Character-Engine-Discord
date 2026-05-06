using CharacterEngineDiscord.Core.Abstractions.Logging;
using CharacterEngineDiscord.Core.Configuration;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Logging;

/// <summary>
/// Default <see cref="IDiscordLogger"/> implementation. Best-effort posts a
/// plain-text copy of the entry to a Discord admin channel — the logs channel
/// for severities below <see cref="LogLevel.Error"/> and the errors channel
/// for <see cref="LogLevel.Error"/>/<see cref="LogLevel.Critical"/>.
/// Reporting never throws — Discord I/O failures are swallowed and logged locally
/// via <see cref="ILogger{T}"/>.
/// </summary>
internal sealed class DiscordLogger : IDiscordLogger
{
    private const int MaxDiscordMessageLength = 1990;

    private readonly ILogger<DiscordLogger> _logger;
    private readonly DiscordShardedClient _discordClient;
    private readonly AdminOptions _adminOptions;

    public DiscordLogger(
        ILogger<DiscordLogger> logger,
        DiscordShardedClient discordClient,
        IOptions<AdminOptions> adminOptions)
    {
        _logger = logger;
        _discordClient = discordClient;
        _adminOptions = adminOptions.Value;
    }

    public async Task ReportAsync(DiscordLogEntry entry, LogLevel severity, CancellationToken cancellationToken = default)
    {
        if (_discordClient.LoginState != LoginState.LoggedIn)
        {
            _logger.LogDebug("ReportAsync skipped: client not yet logged in");
            return;
        }

        var targetId = severity >= LogLevel.Error
            ? _adminOptions.ErrorsChannelId
            : _adminOptions.LogsChannelId;

        if (targetId == 0)
        {
            return;
        }

        if (_discordClient.GetChannel(targetId) is not IMessageChannel channel)
        {
            _logger.LogWarning("ReportAsync: channel {ChannelId} not in cache (severity {Severity})", targetId, severity);
            return;
        }

        var prefix = severity >= LogLevel.Error ? "[ERROR]" : "[INFO]";
        var traceLine = entry.TraceId is null ? string.Empty : $" [{entry.TraceId}]";
        var content = $"{prefix}{traceLine} **{entry.Title}**"
            + (string.IsNullOrEmpty(entry.Message) ? string.Empty : $"\n{entry.Message}");

        if (entry.Exception is not null)
        {
            content += $"\n```\n{entry.Exception}\n```";
        }

        if (content.Length > MaxDiscordMessageLength)
        {
            content = string.Concat(content.AsSpan(0, MaxDiscordMessageLength), "[...]");
        }

        try
        {
            await channel.SendMessageAsync(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReportAsync failed to send to channel {ChannelId}", targetId);
        }
    }
}
