using System.ComponentModel.DataAnnotations;

namespace CharacterEngineDiscord.Core.Configuration;

/// <summary>
/// Top-level bot identity / Discord-gateway authentication options.
/// Bound from configuration section <c>Bot</c>.
/// </summary>
public sealed class BotOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Token { get; init; } = string.Empty;

    public string? PlayingStatus { get; init; }
}
