namespace CharacterEngineDiscord.Models.Abstractions;

public interface IGuildIntegration
{
    public Guid Id { get; set; }

    public string GlobalMessagesFormat { get; set; }

    public DateTime CreatedAt { get; set; }
}
