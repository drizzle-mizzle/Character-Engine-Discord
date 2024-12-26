using System.Collections.Concurrent;
using CharacterEngine.App.Static.Entities;
using CharacterEngineDiscord.Modules.Modules.Chat;
using CharacterEngineDiscord.Modules.Modules.Independent;

namespace CharacterEngine.App.Static;


public static class MemoryStorage
{
    public static HttpClient CommonHttpClient { get; } = new() { MaxResponseContentBufferSize = 5_242_880 };

    /// <summary>
    /// ChannelId : NoWarn
    /// </summary>
    public static ConcurrentDictionary<ulong, bool> CachedChannels { get; } = [];
    public static ConcurrentDictionary<ulong, object?> CachedGuilds { get; } = [];
    public static ConcurrentDictionary<ulong, object?> CachedUsers { get; } = [];
    public static CachedCharacerInfoCollection CachedCharacters { get; } = new();
    public static CachedWebhookClientCollection CachedWebhookClients { get; } = new();

    public static SearchQueryCollection SearchQueries { get; } = new();

    public static IntegrationModulesCollection IntegrationModules { get; } = new();
}


public record IntegrationModulesCollection
{
    public SakuraAiModule SakuraAiModule { get; } = new();

    public CaiModule CaiModule { get; } = new();

    public OpenRouterModule OpenRouterModule { get; } = new();


}
