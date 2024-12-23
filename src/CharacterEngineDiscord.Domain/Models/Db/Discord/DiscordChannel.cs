using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db.Discord;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public sealed class DiscordChannel
{
    public required ulong Id { get; set; }

    [MaxLength(200)]
    public required string ChannelName { get; set; }

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }


    public required bool NoWarn { get; set; }

    [MaxLength(300)]
    public string? MessagesFormat { get; set; }



    public DiscordGuild DiscordGuild { get; set; } = null!;
}
