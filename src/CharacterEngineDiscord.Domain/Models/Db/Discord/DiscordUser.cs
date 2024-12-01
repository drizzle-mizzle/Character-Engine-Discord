using System.ComponentModel.DataAnnotations;

namespace CharacterEngineDiscord.Models.Db.Discord;


public class DiscordUser
{
    [Key]
    public required ulong Id { get; set; }

}
