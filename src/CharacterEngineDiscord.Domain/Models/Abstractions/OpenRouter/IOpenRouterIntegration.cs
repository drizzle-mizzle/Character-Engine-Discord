namespace CharacterEngineDiscord.Domain.Models.Abstractions.OpenRouter;


public interface IOpenRouterIntegration : IOpenRouterConfigurable, IGuildIntegration
{
    public string OpenRouterApiKey { get; set; }
}
