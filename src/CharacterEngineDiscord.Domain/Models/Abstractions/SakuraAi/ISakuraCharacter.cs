namespace CharacterEngineDiscord.Domain.Models.Abstractions.SakuraAi;


public interface ISakuraCharacter : ISolidCharacter, ICharacterCard, ICharacter
{
    public string SakuraDescription { get; set; }
    public string SakuraPersona { get; set; }
    public string SakuraScenario { get; set; }
    public int SakuraMessagesCount { get; set; }
    public string? SakuraChatId { get; set; }
}
