using System.Collections.Concurrent;
using CharacterEngine.App.IntegraionModules;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord.Webhook;

namespace CharacterEngine.App.Helpers.Infrastructure;


public static class StaticStorage
{
    public static HttpClient CommonHttpClient { get; } = new();

    public static SearchQueryCollection SearchQueries { get; } = new();

    public static CachedCharacerInfoCollection CachedCharacters { get; } = new();
    public static CachedWebhookClientCollection CachedWebhookClients { get; } = new();
    public static IntegrationModulesCollection IntegrationModules { get; } = new();
}


public class IntegrationModulesCollection
{
    public SakuraAiModule SakuraAiModule { get; } = new();
}


public class SearchQueryCollection
{
    private readonly ConcurrentDictionary<ulong, SearchQuery> _searchQueries = [];

    public void Add(SearchQuery searchQuery)
    {
        Remove(searchQuery.ChannelId);
        _searchQueries.TryAdd(searchQuery.ChannelId, searchQuery);
    }

    public void Remove(ulong channelId)
    {
        if (_searchQueries.ContainsKey(channelId))
        {
            _searchQueries.TryRemove(channelId, out _);
        }
    }

    public SearchQuery? GetByChannelId(ulong channelId)
    {
        _searchQueries.TryGetValue(channelId, out var searchQuery);
        return searchQuery;
    }
}


public class CachedWebhookClientCollection
{
    private readonly ConcurrentDictionary<ulong, DiscordWebhookClient> _webhookClients = [];

    public void Add(ulong webhookId, DiscordWebhookClient webhookClient)
    {
        Remove(webhookId);
        _webhookClients.TryAdd(webhookId, webhookClient);
    }

    public void Remove(ulong webhookId)
    {
        if (_webhookClients.ContainsKey(webhookId))
        {
            _webhookClients.TryRemove(webhookId, out _);
        }
    }

    public DiscordWebhookClient? GetById(ulong webhookId)
    {
        _webhookClients.TryGetValue(webhookId, out var webhookClient);
        return webhookClient;
    }
}


public class CachedCharacerInfoCollection
{
    private readonly ConcurrentDictionary<Guid, CachedCharacterInfo> _cachedCharacters = [];


    public void AddRange(ICollection<ISpawnedCharacter> spawnedCharacters)
    {
        Parallel.ForEach(spawnedCharacters, Add);
    }

    public void Add(ISpawnedCharacter spawnedCharacter)
    {
        Remove(spawnedCharacter.Id);

        var newCachedCharacter = new CachedCharacterInfo
        {
            Id = spawnedCharacter.Id,
            CallPrefix = spawnedCharacter.CallPrefix,
            ChannelId = spawnedCharacter.DiscordChannelId,
            WebhookId = spawnedCharacter.WebhookId
        };

        _cachedCharacters.TryAdd(spawnedCharacter.Id, newCachedCharacter);
    }

    private void Remove(Guid spawnedCharacterId)
    {
        if (_cachedCharacters.ContainsKey(spawnedCharacterId))
        {
            _cachedCharacters.TryRemove(spawnedCharacterId, out _);
        }
    }

    public CachedCharacterInfo? GetByPrefix(string callPrefix)
    {
        var cachedCaracter = _cachedCharacters.FirstOrDefault(c => c.Value.CallPrefix == callPrefix);

        return cachedCaracter.Key == default ? null : cachedCaracter.Value;
    }

    public List<CachedCharacterInfo> ToList()
        => _cachedCharacters.Values.ToList();
}


public record CachedCharacterInfo
{
    public required Guid Id { get; init; }
    public required ulong ChannelId { get; init; }
    public required string CallPrefix { get; init; }

    public required ulong WebhookId { get; init; }
}
