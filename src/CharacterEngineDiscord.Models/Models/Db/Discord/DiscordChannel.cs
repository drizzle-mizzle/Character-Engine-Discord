using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CharacterEngineDiscord.Models.Db.Discord;


public class DiscordChannel
{
    [Key]
    public required ulong Id { get; set; }

    public required string ChannelName { get; set; }

    [ForeignKey("DiscordGuild")]
    public required ulong DiscordGuildId { get; set; }


    public required bool NoWarn { get; set; }
    public string? MessagesFormat { get; set; }



    public virtual DiscordGuild? DiscordGuild { get; set; }
}
