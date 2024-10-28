using System.ComponentModel.DataAnnotations;

namespace CharacterEngineDiscord.Models.Db;


public class BlockedUser
{
    [Key]
    public required ulong Id { get; set; }

    public required DateTime BlockedAt { get; set; }

}
