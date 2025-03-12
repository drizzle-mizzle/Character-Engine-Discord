using CharacterEngineDiscord.Shared.Abstractions.Characters;

namespace CharacterEngineDiscord.Shared.Abstractions.Sources.SakuraAi;


public interface ISakuraCharacter : IAdoptableCharacter
{
    public string SakuraDescription { get; set; }
    // public string SakuraPersona { get; set; }
    public string SakuraScenario { get; set; }
    public int SakuraMessagesCount { get; set; }
    public string? SakuraChatId { get; set; }
    // public string? SakuraExampleDialog { get; set; }

    CharacterSourceType IAdoptableCharacter.GetCharacterSourceType() => CharacterSourceType.SakuraAI;
}
