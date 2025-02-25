namespace CharacterEngineDiscord.Domain.Models.Abstractions;


public interface IAdoptedCharacter : ICharacter
{
    public CharacterSourceType AdoptedCharacterSourceType { get; }

    public string AdoptedCharacterDefinition { get; }

    public string AdoptedCharacterLink { get; }

    public string AdoptedCharacterAuthorLink { get; }
}
