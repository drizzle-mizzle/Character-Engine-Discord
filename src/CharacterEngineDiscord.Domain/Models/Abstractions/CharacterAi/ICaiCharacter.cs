namespace CharacterEngineDiscord.Domain.Models.Abstractions.CharacterAi;


public interface ICaiCharacter : ISolidCharacter, ICharacter
{
    public string CaiTitle { get; set; }
    public string CaiDescription { get; set; }
    public string? CaiDefinition { get; set; }
    public bool CaiImageGenEnabled { get; set; }
    public int CaiChatsCount { get; set; }

    public string CaiChatId { get; set; }

}
