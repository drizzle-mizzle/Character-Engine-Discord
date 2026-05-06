using Microsoft.EntityFrameworkCore;

namespace CharacterEngineDiscord.Domain.Models.Db.Discord;


[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
public sealed class DiscordUser
{
    public required ulong Id { get; set; }

}
