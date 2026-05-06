using CharacterEngineDiscord.Shared.Models;

namespace CharacterEngineDiscord.Shared.Abstractions.Adapters;


public interface ICharacterAdapter
{
    public CommonCharacter ToCommonCharacter();

    public TResult GetCharacter<TResult>();

    public string GetCharacterName();

    public string GetCharacterLink();

    public string GetAuthorLink();

    public string GetCharacterDescription();

    public IntegrationType GetIntegrationType();

}
