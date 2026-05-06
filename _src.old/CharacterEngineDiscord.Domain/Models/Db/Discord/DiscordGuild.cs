using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db.Discord;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public sealed class DiscordGuild
{
    public required ulong Id { get; set; }

    [MaxLength(100)]
    public required string? GuildName { get; set; }

    public required ulong OwnerId { get; set; }

    [MaxLength(100)]
    public required string? OwnerUsername { get; set; }

    public required int MemberCount { get; set; }

    [MaxLength(300)]
    public string? MessagesFormat { get; set; }

    [MaxLength(2000)]
    public string? SystemPrompt { get; set; }


    public required uint MessagesSent { get; set; }
    public required bool NoWarn { get; set; }


    public required bool Joined { get; set; }
    public required DateTime FirstJoinDate { get; set; }
}
