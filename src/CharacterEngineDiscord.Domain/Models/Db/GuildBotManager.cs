using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Db.Discord;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Models.Db;


[Index(nameof(UserId), nameof(DiscordGuildId), IsUnique = true)]
public class GuildBotManager
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    public required ulong UserId { get; set; }

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }


    /// <summary>
    /// Admin UserId
    /// </summary>
    public required ulong AddedBy { get; set; }


    public virtual DiscordGuild DiscordGuild { get; set; } = null!;
}
