namespace CharacterEngineDiscord.Models.Abstractions.SakuraAi;


public interface ISakuraCharacter : ICharacter
{
    public string SakuraDescription { get; set; }
    public string SakuraPersona { get; set; }
    public string SakuraScenario { get; set; }
    public int SakuraMessagesCount { get; set; }
    public string? SakuraChatId { get; set; }
}
