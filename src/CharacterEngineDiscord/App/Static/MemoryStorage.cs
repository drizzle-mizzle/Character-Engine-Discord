using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.IntegrationModules;

namespace CharacterEngine.App.Static;


public static class MemoryStorage
{
    public static HttpClient CommonHttpClient { get; } = new() { MaxResponseContentBufferSize = 5_242_880 };

    public static SearchQueryCollection SearchQueries { get; } = new();

    public static CachedCharacerInfoCollection CachedCharacters { get; } = new();

    public static CachedWebhookClientCollection CachedWebhookClients { get; } = new();

    public static IntegrationModulesCollection IntegrationModules { get; } = new();
}


public record IntegrationModulesCollection
{
    public SakuraAiModule SakuraAiModule { get; } = new();
}
