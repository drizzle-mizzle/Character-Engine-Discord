namespace CharacterEngineDiscord.Shared.Abstractions.Characters;


public interface IAdoptableCharacter : ICharacter
{
    public CharacterSourceType GetCharacterSourceType();

}
