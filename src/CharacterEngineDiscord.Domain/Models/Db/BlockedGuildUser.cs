using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db;

[PrimaryKey(nameof(UserId), nameof(DiscordGuildId))]
[Index(nameof(UserId), nameof(DiscordGuildId), IsUnique = true)]
public sealed class BlockedGuildUser
{
    public required ulong UserId { get; set; }

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }


    /// <summary>
    /// Admin UserId
    /// </summary>
    public required ulong BlockedBy { get; set; }

    public required DateTime BlockedAt { get; set; }


    public DiscordGuild DiscordGuild { get; set; } = null!;
}
