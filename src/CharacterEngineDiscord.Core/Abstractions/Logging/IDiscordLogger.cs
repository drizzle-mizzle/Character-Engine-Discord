using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.Core.Abstractions.Logging;

/// <summary>
/// Posts an entry to the Discord admin channel matching the severity (logs / errors).
/// Local sinks (console / file) are handled by the standard <see cref="ILogger{T}"/>.
/// </summary>
public interface IDiscordLogger
{
    /// <summary>Sends the entry to the Discord admin channel matching the severity.</summary>
    Task ReportAsync(DiscordLogEntry entry, LogLevel severity, CancellationToken cancellationToken = default);
}
