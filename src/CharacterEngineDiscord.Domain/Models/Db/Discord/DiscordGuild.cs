using System.ComponentModel.DataAnnotations;

namespace CharacterEngineDiscord.Models.Db.Discord;


public class DiscordGuild
{
    [Key]
    public required ulong Id { get; set; }


    public required string? GuildName { get; set; }

    public required ulong OwnerId { get; set; }

    public required string? OwnerUsername { get; set; }

    public required int MemberCount { get; set; }


    public string? MessagesFormat { get; set; }
    public required uint MessagesSent { get; set; }
    public required bool NoWarn { get; set; }


    public required bool Joined { get; set; }
    public required DateTime FirstJoinDate { get; set; }
}
