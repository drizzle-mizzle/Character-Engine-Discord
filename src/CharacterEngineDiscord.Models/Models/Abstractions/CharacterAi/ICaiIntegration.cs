namespace CharacterEngineDiscord.Models.Abstractions.CharacterAi;


public interface ICaiIntegration : IGuildIntegration
{
    public string CaiAuthToken { get; set; }
}
