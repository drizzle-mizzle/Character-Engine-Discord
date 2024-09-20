namespace CharacterEngineDiscord.Models.Abstractions;


public interface ICommonCharacter
{
    public string CharacterId { get; set; }
    public string CharacterName { get; set; }
    public string CharacterDesc { get; set; }
    public string CharacterFirstMessage { get; set; }
    public string? CharacterImageLink { get; set; }
    public int Stat { get; set; }
    public string Author { get; set; }
}
