namespace CharacterEngineDiscord.Db.Models.Abstractions;

public interface ISpawnedCharacter
{
    public Guid Id { get; set; }

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

    public string MessagesFormat { get; set; } // HAS INTEGRATION DEFAULT VALUE

    public uint ResponseDelay { get; set; }

    public float ResponseChance { get; set; }

    public bool EnableQuotes { get; set; }

    public bool EnableStopButton { get; set; }

    public bool SkipNextBotMessage { get; set; }

    /// <summary>
    /// Latest user who called a character
    /// </summary>
    public ulong LastCallerId { get; set; }

    /// <summary>
    /// Latest message that was sent by a character
    /// </summary>
    public ulong LastMessageId { get; set; }

    public uint MessagesSent { get; set; }

    public DateTime LastCallTime { get; set; }
}
