using System.Collections.Concurrent;
using CharacterEngineDiscord.Models;
using Discord.Webhook;
using RestSharp;
using SakuraAi.Client;

namespace CharacterEngine.App;


public static class RuntimeStorage
{
    public static SakuraAiClient SakuraAiClient { get; } = new();
    public static RestClient CommonRestClient { get; } = new();

    public static SearchQueryCollection SearchQueries { get; } = new();

    /// <summary>
    /// Webhook ID : WebhookClient
    /// </summary>
    public static ConcurrentDictionary<ulong, DiscordWebhookClient> WebhookClients { get; } = [];


    public static DiscordWebhookClient? GetWebhookClientById(ulong id)
    {
        WebhookClients.TryGetValue(id, out var webhookClient);
        return webhookClient;
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
            _searchQueries.TryRemove(channelId, out _);
        }


        public SearchQuery? GetByChannelId(ulong channelId)
        {
            _searchQueries.TryGetValue(channelId, out var searchQuery);
            return searchQuery;
        }
    }

}
