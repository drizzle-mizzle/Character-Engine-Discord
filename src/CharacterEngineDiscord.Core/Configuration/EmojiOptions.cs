namespace CharacterEngineDiscord.Core.Configuration;

/// <summary>
/// Discord emoji strings used to badge each integration provider in messages.
/// Bound from configuration section <c>Emoji</c>.
/// </summary>
public sealed class EmojiOptions
{
    public string? Sakura { get; init; }

    public string? Cai { get; init; }

    public string? OpenRouter { get; init; }

    public string? Chub { get; init; }
}
