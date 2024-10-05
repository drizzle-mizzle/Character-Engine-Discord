using CharacterEngineDiscord.Models.Db.Discord;

namespace CharacterEngineDiscord.Models.Abstractions;

public interface IIntegration
{
    public Guid Id { get; set; }

    public string GlobalMessagesFormat { get; set; }

    public DateTime CreatedAt { get; set; }
}
