namespace CharacterEngineDiscord.Models.Abstractions.CharacterAi;


public interface ICaiCharacter : ICharacter
{
    public string CaiTitle { get; set; }
    public string CaiDescription { get; set; }
    public string? CaiDefinition { get; set; }
    public bool CaiImageGenEnabled { get; set; }
    public int CaiChatsCount { get; set; }

}
