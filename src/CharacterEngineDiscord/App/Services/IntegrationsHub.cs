using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Modules.Abstractions;
using CharacterEngineDiscord.Modules.Modules.Chat;
using CharacterEngineDiscord.Modules.Modules.Search;
using CharacterEngineDiscord.Modules.Modules.Universal;
using CharacterEngineDiscord.Shared;

namespace CharacterEngine.App.Infrastructure;


public static class IntegrationsHub
{
    public static SakuraAiModule SakuraAiModule { get; } = new();

    public static CaiModule CharacterAiModule { get; } = new();

    public static OpenRouterModule OpenRouterModule { get; } = new(BotConfig.DATABASE_CONNECTION_STRING, BotConfig.DEFAULT_SYSTEM_PROMPT);

    public static ChubAiModule ChubAiModule { get; } = new();


    public static IChatModule GetChatModule(IntegrationType integrationType)
        => GetIntegrationModule<IChatModule>(integrationType);

    public static ISearchModule GetSearchModule(IntegrationType integrationType)
        => GetIntegrationModule<ISearchModule>(integrationType);

    public static ISearchModule GetSearchModule(CharacterSourceType characterSourceType)
        => GetIntegrationModule<ISearchModule>(characterSourceType);


    private static TResult GetIntegrationModule<TResult>(IntegrationType integrationType) where TResult : IModule
    {
        return (TResult)(IModule)(integrationType switch
        {
            IntegrationType.SakuraAI => SakuraAiModule,
            IntegrationType.CharacterAI => CharacterAiModule,
            IntegrationType.OpenRouter => OpenRouterModule,

            _ => throw new ArgumentOutOfRangeException(nameof(integrationType), integrationType, null)
        });
    }

    private static TResult GetIntegrationModule<TResult>(CharacterSourceType characterSourceType) where TResult : IModule
    {
        return (TResult)(IModule)(characterSourceType switch
        {
            CharacterSourceType.SakuraAI => SakuraAiModule,
            CharacterSourceType.ChubAI => ChubAiModule,

            _ => throw new ArgumentOutOfRangeException(nameof(characterSourceType), characterSourceType, null)
        });
    }

}
