namespace CharacterEngineDiscord.Models.Abstractions.SakuraAi;


public interface ISakuraIntegration : IGuildIntegration
{
    public string SakuraEmail { get; set; }

    public string SakuraSessionId { get; set; }

    public string SakuraRefreshToken { get; set; }
}
