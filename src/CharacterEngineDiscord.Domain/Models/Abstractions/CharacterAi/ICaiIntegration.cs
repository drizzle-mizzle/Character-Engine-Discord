namespace CharacterEngineDiscord.Domain.Models.Abstractions.CharacterAi;


public interface ICaiIntegration : IGuildIntegration
{
    public string? CaiEmail { get; set; }
    public string CaiAuthToken { get; set; }
    public string CaiUserId { get; set; }
    public string CaiUsername { get; set; }

}
