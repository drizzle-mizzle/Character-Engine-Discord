namespace CharacterEngineDiscord.Domain.Models.Abstractions;


public interface IReusableCharacter : ICharacter
{
    public string GetCharacterDefinition();

    public CharacterSourceType GetCharacterSourceType();
}
