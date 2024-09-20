using CharacterEngineDiscord.Db.Models.Abstractions;

namespace CharacterEngineDiscord.Db.Models.Db.SpawnedCharacters;


public class SakuraAiSpawnedCharacter : ISpawnedCharacter
{
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

    public required string CharacterId { get; set; }
    public required string CharacterName { get; set; }
    public required string CharacterDesc { get; set; }
    public required string CharacterFirstMessage { get; set; }
    public required string? CharacterImageLink { get; set; }
}
