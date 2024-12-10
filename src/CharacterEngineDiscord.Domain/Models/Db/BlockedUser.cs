using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Models.Db;


[Index(nameof(Id), IsUnique = true)]
public class BlockedUser
{
    [Key]
    public required ulong Id { get; set; }

    public required DateTime BlockedAt { get; set; }

    public required DateTime BlockedUntil { get; set; }

}
