using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Db;


public class BlockedGuildUser
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }

    public required ulong UserId { get; set; }

    /// <summary>
    /// Admin UserId
    /// </summary>
    public required ulong BlockedBy { get; set; }

    public required DateTime BlockedAt { get; set; }


    public virtual DiscordGuild DiscordGuild { get; set; } = null!;
}
