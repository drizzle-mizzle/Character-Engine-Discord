namespace CharacterEngineDiscord.Shared.Abstractions;


public interface IChatOnlyIntegration : IIntegration
{
    public string SystemPrompt { get; }
}
