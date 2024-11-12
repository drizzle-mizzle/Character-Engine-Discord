using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Db.SpawnedCharacters;


public class CaiSpawnedCharacter : ICaiCharacter, ISpawnedCharacter
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [ForeignKey("DiscordChannel")]
    public ulong DiscordChannelId { get; set; }

    public ulong WebhookId { get; set; }
    public string WebhookToken { get; set; } = null!;
    public string CallPrefix { get; set; } = null!;
    public string? MessagesFormat { get; set; }
    public uint ResponseDelay { get; set; }
    public double FreewillFactor { get; set; }

    public uint FreewillContextSize { get; set; }
    public bool EnableSwipes { get; set; }
    public bool EnableWideContext { get; set; }
    public bool EnableQuotes { get; set; }
    public bool EnableStopButton { get; set; }
    public bool SkipNextBotMessage { get; set; }
    public bool ResetWithNextMessage { get; set; }
    public ulong LastCallerDiscordUserId { get; set; }
    public ulong LastDiscordMessageId { get; set; }
    public uint MessagesSent { get; set; }
    public DateTime LastCallTime { get; set; }

    public string CharacterId { get; set; } = null!;
    public string CharacterName { get; set; } = null!;
    public string CharacterFirstMessage { get; set; } = null!;
    public string? CharacterImageLink { get; set; }
    public string CharacterAuthor { get; set; } = null!;
    public bool IsNfsw { get; set; }
    public string CharacterStat => CaiChatsCount.ToString();

    public string CaiTitle { get; set; } = string.Empty;
    public string CaiDescription { get; set; } = string.Empty;
    public string? CaiDefinition { get; set; }
    public bool CaiImageGenEnabled { get; set; }
    public int CaiChatsCount { get; set; }

    public string? CaiChatId { get; set; }

    public virtual DiscordChannel? DiscordChannel { get; set; } = null!;

}
