namespace CharacterEngineDiscord.Models;


public record CommonCharacter
{
    public required IntegrationType IntegrationType { get; set; }
    public required string CharacterId { get; set; }
    public required string Name { get; set; }
    public required string Desc { get; set; }
    public required string FirstMessage { get; set; }
    public required string Author { get; set; }
    public required string? ImageLink { get; set; }
    public float? Stat { get; set; }
    public string? OriginalLink { get; set; }

    public dynamic? OriginalCharacterModel { get; set; }
}
