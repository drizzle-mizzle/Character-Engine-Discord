using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Common;

namespace CharacterEngineDiscord.Modules.Abstractions;


public interface ICharacterAdapter
{
    public CommonCharacter ToCommonCharacter();

    public IReusableCharacter ToReusableCharacter();

    public TResult GetValue<TResult>();

    public IntegrationType GetIntegrationType();
}
