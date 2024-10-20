using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Db;


public class Manager
{
    [Key]
    public Guid Id { get; set; }

    [ForeignKey("DiscordGuild")]
    public ulong GuildId { get; set; }

    public ulong UserId { get; set; }



    public virtual DiscordGuild DiscordGuild { get; set; } = null!;
}
