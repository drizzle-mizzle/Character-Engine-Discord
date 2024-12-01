using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Abstractions.CharacterAi;
using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Db.Integrations;


public class CaiGuildIntegration : ICaiIntegration
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }

    public string? GlobalMessagesFormat { get; set; } = null;
    public required DateTime CreatedAt { get; set; }

    public required string CaiEmail { get; set; }
    public required string CaiAuthToken { get; set; }
    public required string CaiUserId { get; set; }
    public required string CaiUsername { get; set; }


    public virtual DiscordGuild DiscordGuild { get; set; } = null!;
}
