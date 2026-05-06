using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db;


[PrimaryKey(nameof(Id), nameof(SpawnedCharacterId))]
[Index(nameof(Id), nameof(SpawnedCharacterId), IsUnique = false)]
public class CharacterChatHistory
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    public required Guid SpawnedCharacterId { get; set; }

    [MaxLength(50)]
    public required string Name { get; set; }

    [MaxLength(int.MaxValue)]
    public required string Message { get; set; }

    public required DateTime CreatedAt { get; set; }
}
