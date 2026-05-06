using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public class BlockedUser
{
    public required ulong Id { get; set; }

    public required DateTime BlockedAt { get; set; }

    public required DateTime BlockedUntil { get; set; }

}
