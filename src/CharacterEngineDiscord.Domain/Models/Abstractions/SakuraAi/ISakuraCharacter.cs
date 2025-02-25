namespace CharacterEngineDiscord.Domain.Models.Abstractions.SakuraAi;


public interface ISakuraCharacter : IReusableCharacter
{
    public string SakuraDescription { get; set; }
    public string SakuraPersona { get; set; }
    public string SakuraScenario { get; set; }
    public int SakuraMessagesCount { get; set; }
    public string? SakuraChatId { get; set; }

    public string? SakuraExampleDialog { get; set; }
}
