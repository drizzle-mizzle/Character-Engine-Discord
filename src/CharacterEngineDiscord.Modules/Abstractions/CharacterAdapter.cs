using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Abstractions;
using CharacterEngineDiscord.Domain.Models.Common;

namespace CharacterEngineDiscord.Modules.Abstractions;


public abstract class CharacterAdapter<T> : ICharacterAdapter
{
    public abstract T Value { get; }
    public abstract CommonCharacter ToCommonCharacter();
    public abstract IntegrationType GetIntegrationType();


    public virtual IReusableCharacter ToReusableCharacter()
        => throw new NotImplementedException();


    TResult ICharacterAdapter.GetValue<TResult>() => (TResult)Convert.ChangeType(Value, typeof(TResult))!;
}


public interface ICharacterAdapter
{
    public CommonCharacter ToCommonCharacter();

    public IReusableCharacter ToReusableCharacter();

    public TResult GetValue<TResult>();

    public IntegrationType GetIntegrationType();
}
