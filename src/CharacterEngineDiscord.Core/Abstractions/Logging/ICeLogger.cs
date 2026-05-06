namespace CharacterEngineDiscord.Core.Abstractions.Logging;

/// <summary>
/// Bot-wide logging facade combining local sinks (console/file) with remote
/// admin-channel reporting.
/// </summary>
public interface ICeLogger
{
    /// <summary>Writes the entry to local sinks (console / file).</summary>
    void Log(CeLogEntry entry, CeLogSeverity severity);

    /// <summary>Sends the entry to the Discord admin channel.</summary>
    Task ReportAsync(CeLogEntry entry, CeLogSeverity severity, CancellationToken cancellationToken = default);
}
