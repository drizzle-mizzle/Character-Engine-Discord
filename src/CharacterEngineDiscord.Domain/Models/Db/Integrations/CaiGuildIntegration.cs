using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using CharacterEngineDiscord.Shared.Abstractions.Sources.CharacterAi;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db.Integrations;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public sealed class CaiGuildIntegration : ICaiIntegration, IGuildIntegration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }

    [MaxLength(300)]
    public string? GlobalMessagesFormat { get; set; }

    public required DateTime CreatedAt { get; set; }

    [MaxLength(100)]
    public required string CaiEmail { get; set; }

    [MaxLength(100)]
    public required string CaiAuthToken { get; set; }

    [MaxLength(20)]
    public required string CaiUserId { get; set; }

    [MaxLength(100)]
    public required string CaiUsername { get; set; }


    public DiscordGuild DiscordGuild { get; set; } = null!;

    public bool IsChatOnly
        => false;
}
