using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Db;


public class DiscordGuildIntegration
{
    [Key]
    public required Guid Id { get; set; }

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }

    public required Guid IntegraionId { get; set; }
    public required IntegrationType IntegrationType { get; set; }


    public virtual DiscordGuild? DiscordGuild { get; set; }
}
