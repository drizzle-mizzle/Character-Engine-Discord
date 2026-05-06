namespace CharacterEngineDiscord.Core.Abstractions.Logging;

/// <summary>
/// Payload posted to a Discord admin channel by <see cref="IDiscordLogger"/>.
/// </summary>
public sealed record DiscordLogEntry
{
    public required string Title { get; init; }

    public string? Message { get; init; } = null;

    public Exception? Exception { get; init; } = null;

    public string? TraceId { get; init; } = null;
}
