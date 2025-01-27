using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db;

[PrimaryKey(nameof(UserOrRoleId), nameof(DiscordGuildId))]
[Index(nameof(UserOrRoleId), nameof(DiscordGuildId), IsUnique = true)]
public sealed class BlockedGuildUser
{
    public required ulong UserOrRoleId { get; set; }
    
    public required bool IsRole { get; set; }

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }


    /// <summary>
    /// Admin UserId
    /// </summary>
    public required ulong BlockedBy { get; set; }

    public required DateTime BlockedAt { get; set; }


    public DiscordGuild DiscordGuild { get; set; } = null!;
}
