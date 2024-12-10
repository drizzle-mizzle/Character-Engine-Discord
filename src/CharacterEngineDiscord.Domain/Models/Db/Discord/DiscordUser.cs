using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Models.Db.Discord;


[Index(nameof(Id), IsUnique = true)]
public class DiscordUser
{
    [Key]
    public required ulong Id { get; set; }

}
