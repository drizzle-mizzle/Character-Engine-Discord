using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Domain.Models.Db.Discord;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db;


[PrimaryKey(nameof(DiscordUserOrRoleId), nameof(DiscordGuildId))]
[Index(nameof(DiscordUserOrRoleId), nameof(DiscordGuildId), IsUnique = true)]
public sealed class GuildBotManager
{
    // Or role ID
    public required ulong DiscordUserOrRoleId { get; set; }

    public required bool IsRole { get; set; } = false;

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }


    /// <summary>
    /// Admin UserId
    /// </summary>
    public required ulong AddedBy { get; set; }


    public DiscordGuild DiscordGuild { get; set; } = null!;
}
