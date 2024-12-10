using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Models.Db.Discord;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Models.Db.Integrations;


[Index(nameof(Id), IsUnique = true)]
public class SakuraAiGuildIntegration : ISakuraIntegration
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }

    [MaxLength(300)]
    public string? GlobalMessagesFormat { get; set; } = null;

    public required DateTime CreatedAt { get; set; }


    [MaxLength(100)]
    public required string SakuraEmail { get; set; }

    [MaxLength(100)]
    public required string SakuraSessionId { get; set; }

    [MaxLength(800)]
    public required string SakuraRefreshToken { get; set; }



    public virtual DiscordGuild DiscordGuild { get; set; } = null!;
}
