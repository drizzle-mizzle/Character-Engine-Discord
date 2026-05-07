using System.ComponentModel.DataAnnotations;

namespace CharacterEngineDiscord.Core.Configuration;

/// <summary>
/// Per-user interaction rate limits and escalating block durations.
/// Bound from configuration section <c>RateLimit</c>.
/// </summary>
public sealed class RateLimitOptions
{
    [Range(1, int.MaxValue)]
    public int PerWindow { get; init; }

    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; init; } = 30;

    public int FirstBlockMinutes { get; init; }

    public int SecondBlockHours { get; init; }
}
