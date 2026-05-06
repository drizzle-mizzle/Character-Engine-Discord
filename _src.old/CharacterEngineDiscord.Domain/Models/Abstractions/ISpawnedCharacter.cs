using CharacterEngineDiscord.Domain.Models.Db.Discord;
using CharacterEngineDiscord.Shared.Abstractions.Characters;

namespace CharacterEngineDiscord.Domain.Models.Abstractions;


/// <summary>
/// Character spawned in DiscordChannel
/// </summary>
public interface ISpawnedCharacter : ICharacter
{
    public Guid Id { get; }

    public ulong DiscordChannelId { get; set; }

    /// <summary>
    /// Associated Discord webhook ID
    /// </summary>
    public ulong WebhookId { get; set; }

    /// <summary>
    /// Associated Discord webhook token
    /// </summary>
    public string WebhookToken { get; set; }

    /// <summary>
    /// Some prefix the character will respond on
    /// </summary>
    public string CallPrefix { get; set; }

    public string? MessagesFormat { get; set; } // HAS INTEGRATION DEFAULT VALUE

    public uint ResponseDelay { get; set; }

    public double FreewillFactor { get; set; }


    public uint FreewillContextSize { get; set; }

    public bool EnableSwipes { get; set; }


    public bool EnableQuotes { get; set; }

    public bool EnableStopButton { get; set; }

    public bool SkipNextBotMessage { get; set; }


    /// <summary>
    /// Latest user who called a character
    /// </summary>
    public ulong LastCallerDiscordUserId { get; set; }

    /// <summary>
    /// Latest message that was sent by a character
    /// </summary>
    public ulong LastDiscordMessageId { get; set; }

    public uint MessagesSent { get; set; }

    public DateTime LastCallTime { get; set; }

    public DiscordChannel? DiscordChannel { get; set; }

}
