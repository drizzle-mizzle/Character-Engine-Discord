using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Models.Db;


[Index(nameof(DiscordUserId), nameof(SpawnedCharacterId), IsUnique = true)]
public class HuntedUser
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    public required ulong DiscordUserId { get; set; }
    public required Guid SpawnedCharacterId { get; set; }
}
