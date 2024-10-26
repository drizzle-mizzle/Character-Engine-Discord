using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Db.SpawnedCharacters;


public class SakuraAiSpawnedCharacter : ISpawnedCharacter, ICharacter, ISakuraCharacter
{
    [Key]
    public Guid Id { get; } = Guid.NewGuid();

    [ForeignKey("DiscordChannel")]
    public required ulong DiscordChannelId { get; set; }

    public required ulong WebhookId { get; set; }
    public required string WebhookToken { get; set; }
    public required string CallPrefix { get; set; }
    public required string MessagesFormat { get; set; }
    public required uint ResponseDelay { get; set; }
    public required float ResponseChance { get; set; }
    public required bool EnableSwipes { get; set; }
    public required bool EnableBuffering { get; set; }
    public required bool EnableQuotes { get; set; }
    public required bool EnableStopButton { get; set; }
    public required bool SkipNextBotMessage { get; set; }
    public required bool ResetWithNextMessage { get; set; }
    public required ulong LastCallerId { get; set; }
    public required ulong LastMessageId { get; set; }
    public required uint MessagesSent { get; set; }
    public required DateTime LastCallTime { get; set; }

    public string CharacterId { get; set; } = null!;
    public string CharacterName { get; set; } = null!;
    public string CharacterFirstMessage { get; set; } = null!;
    public string? CharacterImageLink { get; set; }
    public string CharacterAuthor { get; set; } = null!;
    public string CharacterStat => SakuraMessagesCount.ToString();

    public string SakuraDescription { get; set; } = string.Empty;
    public string SakuraPersona { get; set; } = string.Empty;
    public string SakuraScenario { get; set; } = string.Empty;
    public int SakuraMessagesCount { get; set; }
    public string? SakuraChatId { get; set; }


    public virtual DiscordChannel DiscordChannel { get; set; } = null!;
}
