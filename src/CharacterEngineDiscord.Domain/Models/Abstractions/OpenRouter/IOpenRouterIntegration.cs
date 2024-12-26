namespace CharacterEngineDiscord.Domain.Models.Abstractions.OpenRouter;


public interface IOpenRouterIntegration : IOpenRouterSettings, IGuildIntegration
{
    public string OpenRouterApiKey { get; set; }
}
