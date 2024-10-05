using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CharacterEngineDiscord.Models.Abstractions;
using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Db.Integrations;


public class SakuraAiIntegration : IIntegration
{
    // Base
    [Key]
    public required Guid Id { get; set; }

    public required string GlobalMessagesFormat { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Own
    public required string Email { get; set; }
    public required string RefreshToken { get; set; }

}
