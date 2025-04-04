namespace CharacterEngineDiscord.Shared.Abstractions.Characters;


public interface ICharacter
{
    public string CharacterId { get; set; }
    public string CharacterName { get; set; }
    public string CharacterFirstMessage { get; set; }
    public string? CharacterImageLink { get; set; }
    public string CharacterAuthor { get; set; }
    public bool IsNfsw { get; set; }
}
