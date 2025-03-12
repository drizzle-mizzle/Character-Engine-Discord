using CharacterEngineDiscord.Shared.Abstractions.Characters;

namespace CharacterEngineDiscord.Shared.Abstractions.Sources.CharacterAi;


public interface ICaiCharacter : ICharacter
{
    public string CaiTitle { get; set; }
    public string CaiDescription { get; set; }
    public bool CaiImageGenEnabled { get; set; }
    public int CaiChatsCount { get; set; }

    public string? CaiChatId { get; set; }

}
