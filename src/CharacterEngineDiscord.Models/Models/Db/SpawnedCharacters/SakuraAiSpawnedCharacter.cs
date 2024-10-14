using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Db.Discord;
using SakuraAi.Client.Models.Common;

namespace CharacterEngineDiscord.Models.Db.SpawnedCharacters;


public class SakuraAiSpawnedCharacter : ISpawnedCharacter
{
    [Key]
    public required Guid Id { get; set; }

    [ForeignKey("DiscordChannel")]
    public required ulong DiscordChannelId { get; set; }

    public required ulong WebhookId { get; set; }
    public required string WebhookToken { get; set; }
    public required string CallPrefix { get; set; }
    public required string MessagesFormat { get; set; }
    public required uint ResponseDelay { get; set; }
    public required float ResponseChance { get; set; }
    public required bool EnableQuotes { get; set; }
    public required bool EnableStopButton { get; set; }
    public required bool SkipNextBotMessage { get; set; }
    public bool ResetWithNextMessage { get; set; } = true;
    public required ulong LastCallerId { get; set; }
    public required ulong LastMessageId { get; set; }
    public required uint MessagesSent { get; set; }
    public required DateTime LastCallTime { get; set; }

    public string CharacterId { get; set; }
    public string CharacterName { get; set; }
    public string CharacterFirstMessage { get; set; }
    public string? CharacterImageLink { get; set; }
    public string CharacterAuthor { get; set; }

    // Own
    public string SakuraDescription { get; set; }
    public string SakuraPersona { get; set; }
    public string SakuraScenario { get; set; }
    public float SakuraConverstaionsCount { get; set; } = 0;
    public string? SakuraChatId { get; set; } = null;



    public virtual DiscordChannel DiscordChannel { get; set; }
}
