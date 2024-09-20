using CharacterEngineDiscord.Db.Models.Abstractions;

namespace CharacterEngineDiscord.Db.Models.Db.Integrations;


public class SakuraAiGuildIntegration : IGuildIntegration
{
    // Base
    public required Guid Id { get; set; }
    public required string GlobalMessagesFormat { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Own
    public required string Email { get; set; }
    public required string RefreshToken { get; set; }
}
