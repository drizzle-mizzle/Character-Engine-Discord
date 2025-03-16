namespace CharacterEngineDiscord.Shared.Abstractions.Sources.OpenRouter;


public interface IOpenRouterIntegration : IOpenRouterConfigurable, IChatOnlyIntegration
{
    public string OpenRouterApiKey { get; }
}
