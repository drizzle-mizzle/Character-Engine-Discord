namespace CharacterEngineDiscord.Models.Abstractions;

public interface IGuildIntegration
{
    public Guid Id { get; }

    public string GlobalMessagesFormat { get; set; }

    public DateTime CreatedAt { get; set; }
}
