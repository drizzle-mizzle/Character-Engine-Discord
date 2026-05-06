namespace CharacterEngineDiscord.Shared.Abstractions.Characters;


public interface IAdoptedCharacter : ICharacter
{
    public CharacterSourceType AdoptedCharacterSourceType { get; }

    public string? AdoptedCharacterSystemPrompt { get; set; }

    public string AdoptedCharacterDefinition { get; }

    public string AdoptedCharacterDescription { get; }

    public string AdoptedCharacterLink { get; }

    public string AdoptedCharacterAuthorLink { get; }
}
