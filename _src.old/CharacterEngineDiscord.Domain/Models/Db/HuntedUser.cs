using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db;


[PrimaryKey(nameof(DiscordUserId), nameof(SpawnedCharacterId))]
[Index(nameof(DiscordUserId), nameof(SpawnedCharacterId), IsUnique = true)]
public class HuntedUser
{
    public required ulong DiscordUserId { get; set; }
    public required Guid SpawnedCharacterId { get; set; }
}
