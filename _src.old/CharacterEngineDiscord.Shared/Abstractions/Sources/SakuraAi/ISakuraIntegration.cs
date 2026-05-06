namespace CharacterEngineDiscord.Shared.Abstractions.Sources.SakuraAi;


public interface ISakuraIntegration : IIntegration
{
    public string SakuraEmail { get; set; }

    public string SakuraSessionId { get; set; }

    public string SakuraRefreshToken { get; set; }
}
