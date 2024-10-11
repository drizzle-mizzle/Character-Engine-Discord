using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Db;


public class DiscordChannelSpawnedCharacter
{
    [Key]
    public required Guid Id { get; set; }

    [ForeignKey("DiscordChannel")]
    public required ulong DiscordChannelId { get; set; }

    public required Guid SpawnedCharacterId { get; set; }
    public required IntegrationType IntegrationType { get; set; }


    public virtual DiscordChannel? DiscordChannel { get; set; }
}
