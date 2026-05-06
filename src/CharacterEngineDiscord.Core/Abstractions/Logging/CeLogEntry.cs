namespace CharacterEngineDiscord.Core.Abstractions.Logging;

/// <summary>
/// Structured log payload carried by <see cref="ICeLogger"/>.
/// </summary>
public sealed record CeLogEntry
{
    public required string Title { get; init; }

    public string? Message { get; init; } = null;

    public Exception? Exception { get; init; } = null;

    public string? TraceId { get; init; } = null;
}
