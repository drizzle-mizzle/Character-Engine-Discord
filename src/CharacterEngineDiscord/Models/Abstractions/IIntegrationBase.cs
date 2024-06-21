namespace CharacterEngine.Models.Abstractions;

public interface IIntegrationBase
{
    public Guid Id { get; set; }

    public string GlobalMessagesFormat { get; set; }

    public DateTime CreatedAt { get; set; }
}
