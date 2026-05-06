namespace CharacterEngineDiscord.Core.Abstractions.Logging;

/// <summary>
/// Bot-side log severities. Numeric values mirror
/// <see cref="Microsoft.Extensions.Logging.LogLevel"/> for direct casting.
/// </summary>
public enum CeLogSeverity
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
}
