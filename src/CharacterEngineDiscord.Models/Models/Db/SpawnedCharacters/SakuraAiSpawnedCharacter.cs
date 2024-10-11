using System.ComponentModel.DataAnnotations;
using CharacterEngineDiscord.Models.Abstractions;

namespace CharacterEngineDiscord.Models.Db.SpawnedCharacters;


public class SakuraAiSpawnedCharacter : ISpawnedCharacter
{
    private float _characterStat;

    [Key]
    public required Guid Id { get; set; }

    public required ulong WebhookId { get; set; }
    public required string WebhookToken { get; set; }
    public required string CallPrefix { get; set; }
    public required string MessagesFormat { get; set; }
    public required uint ResponseDelay { get; set; }
    public required float ResponseChance { get; set; }
    public required bool EnableQuotes { get; set; }
    public required bool EnableStopButton { get; set; }
    public required bool SkipNextBotMessage { get; set; }
    public required ulong LastCallerId { get; set; }
    public required ulong LastMessageId { get; set; }
    public required uint MessagesSent { get; set; }
    public required DateTime LastCallTime { get; set; }

    public string CharacterId { get; set; }
    public string CharacterName { get; set; }
    public string CharacterDesc { get; set; }
    public string CharacterFirstMessage { get; set; }
    public string? CharacterImageLink { get; set; }
    public string CharacterAuthor { get; set; }

    public float? CharacterStat
    {
        get => SakuraConverstaionsCount;
        set => SakuraConverstaionsCount = value ?? 0;
    }

    // Own
    public float SakuraConverstaionsCount { get; set; }
}
