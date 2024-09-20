namespace CharacterEngineDiscord.Models.Db;


public class DiscordChannelSpawnedCharacter
{
    public required Guid Id { get; set; }
    public required ulong DiscordChannelId { get; set; }
    public required Guid SpawnedCharacterId { get; set; }
    public required IntegrationType IntegrationType { get; set; }
}
