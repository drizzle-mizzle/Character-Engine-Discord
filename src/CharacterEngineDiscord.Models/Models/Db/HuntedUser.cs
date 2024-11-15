using System.ComponentModel.DataAnnotations;

namespace CharacterEngineDiscord.Models.Db;


public class HuntedUser
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    public required ulong DiscordUserId { get; set; }
    public required Guid SpawnedCharacterId { get; set; }
}
