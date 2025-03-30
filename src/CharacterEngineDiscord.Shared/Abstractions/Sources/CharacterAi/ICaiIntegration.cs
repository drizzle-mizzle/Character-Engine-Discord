namespace CharacterEngineDiscord.Shared.Abstractions.Sources.CharacterAi;


public interface ICaiIntegration : IIntegration
{
    public string CaiEmail { get; set; }
    public string CaiAuthToken { get; set; }
    public string CaiUserId { get; set; }
    public string CaiUsername { get; set; }

}
