using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db;


[PrimaryKey(nameof(DiscordUserId), nameof(DiscordGuildId))]
[Index(nameof(DiscordUserId), nameof(DiscordGuildId), IsUnique = true)]
public sealed class GuildBotManager
{
    public required ulong DiscordUserId { get; set; }

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }


    /// <summary>
    /// Admin UserId
    /// </summary>
    public required ulong AddedBy { get; set; }


    public DiscordGuild DiscordGuild { get; set; } = null!;
}
