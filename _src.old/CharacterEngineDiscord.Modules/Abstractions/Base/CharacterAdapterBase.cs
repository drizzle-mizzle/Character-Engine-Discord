using CharacterEngineDiscord.Shared;
using CharacterEngineDiscord.Shared.Abstractions.Adapters;
using CharacterEngineDiscord.Shared.Models;

namespace CharacterEngineDiscord.Modules.Abstractions.Base;


public abstract class CharacterAdapter<T> : CharacterAdapterBase<T>
{
    protected CharacterAdapter(IntegrationType integrationType) : base(integrationType)
    {

    }
}


public abstract class AdoptableCharacterAdapter<T> : CharacterAdapterBase<T>, IAdoptableCharacterAdapter
{
    private readonly CharacterSourceType _characterSourceType;


    protected AdoptableCharacterAdapter(IntegrationType integrationType, CharacterSourceType characterSourceType)
        : base(integrationType)
    {
        _characterSourceType = characterSourceType;
    }


    public CharacterSourceType GetCharacterSourceType()
        => _characterSourceType;

    public abstract string GetCharacterDefinition();
}


public abstract class CharacterAdapterBase<T> : ICharacterAdapter
{
    private readonly IntegrationType _integrationType;

    protected abstract T Character { get; }


    protected CharacterAdapterBase(IntegrationType integrationType)
    {
        _integrationType = integrationType;
    }


    public IntegrationType GetIntegrationType()
        => _integrationType;

    public abstract CommonCharacter ToCommonCharacter();

    public abstract string GetCharacterName();

    public abstract string GetCharacterLink();

    public abstract string GetAuthorLink();

    public abstract string GetCharacterDescription();


    TResult ICharacterAdapter.GetCharacter<TResult>() => (TResult)Convert.ChangeType(Character, typeof(TResult))!;
}