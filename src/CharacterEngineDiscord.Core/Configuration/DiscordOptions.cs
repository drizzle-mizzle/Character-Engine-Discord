namespace CharacterEngineDiscord.Core.Configuration;

/// <summary>
/// Discord client tuning knobs (gateway cache size, connection timeouts).
/// Bound from configuration section <c>Discord</c>.
/// </summary>
public sealed class DiscordOptions
{
    public int MessageCacheSize { get; init; } = 10;

    public int ConnectionTimeoutMs { get; init; }
}
