using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Db;


public class GuildBotManager
{
    [Key]
    public Guid Id { get; } = Guid.NewGuid();

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }

    public required ulong UserId { get; set; }

    /// <summary>
    /// Admin UserId
    /// </summary>
    public required ulong AddedBy { get; set; }


    public virtual DiscordGuild DiscordGuild { get; set; } = null!;
}
