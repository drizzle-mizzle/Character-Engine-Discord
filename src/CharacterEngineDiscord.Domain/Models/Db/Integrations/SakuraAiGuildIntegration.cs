using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Domain.Models.Abstractions.SakuraAi;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db.Integrations;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public sealed class SakuraAiGuildIntegration : ISakuraIntegration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }

    [MaxLength(300)]
    public string? GlobalMessagesFormat { get; set; }

    public required DateTime CreatedAt { get; set; }


    [MaxLength(100)]
    public required string SakuraEmail { get; set; }

    [MaxLength(100)]
    public required string SakuraSessionId { get; set; }

    [MaxLength(800)]
    public required string SakuraRefreshToken { get; set; }


    public DiscordGuild DiscordGuild { get; set; } = null!;
}
