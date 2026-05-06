namespace CharacterEngineDiscord.Shared.Abstractions.Adapters;


public interface IAdoptableCharacterAdapter : ICharacterAdapter
{
    public CharacterSourceType GetCharacterSourceType();
    public string GetCharacterDefinition();

}
