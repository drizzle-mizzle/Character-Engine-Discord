using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Db.Integrations;


public class SakuraAiGuildIntegration : IGuildIntegration, ISakuraIntegration
{
    [Key]
    public Guid Id { get; } = Guid.NewGuid();

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }

    public required string GlobalMessagesFormat { get; set; }
    public required DateTime CreatedAt { get; set; }

    public required string SakuraEmail { get; set; }
    public required string SakuraSessionId { get; set; }
    public required string SakuraRefreshToken { get; set; }


    public virtual DiscordGuild DiscordGuild { get; set; } = null!;
}
