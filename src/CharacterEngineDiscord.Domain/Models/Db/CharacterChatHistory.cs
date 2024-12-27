using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db;


[PrimaryKey(nameof(SpawnedCharacterId), nameof(CreatedAt))]
[Index(nameof(SpawnedCharacterId), IsUnique = false)]
public class CharacterChatHistory
{
    public required Guid SpawnedCharacterId { get; init; }
    public required DateTime CreatedAt { get; init; }

    [MaxLength(50)]
    public required string Name { get; init; }

    public required string Message { get; set; }
}
