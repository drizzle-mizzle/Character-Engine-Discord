namespace CharacterEngineDiscord.Domain.Models.Abstractions.OpenRouter;


public interface IOpenRouterCharacter : ICharacterCard, IOpenRouterSettings, ICharacter
{
    public CharacterSourceType CharacterSourceType { get; set; }
}
