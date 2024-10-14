using System.Collections.Concurrent;
using CharacterEngine.App.Modules;
using CharacterEngineDiscord.Models;
using Discord.Webhook;
using RestSharp;

namespace CharacterEngine.App.Helpers.Infrastructure;


public static class RuntimeStorage
{
    public static ConcurrentBag<string> CharacterPrefixes { get; } = [];
    public static SakuraAiModule SakuraAiModule { get; } = new();

    public static RestClient CommonRestClient { get; } = new();

    public static SearchQueryConcurrentCollection SearchQueries { get; } = new();

    public static WebhooksClientConcurrentCollection WebhookClients { get; } = new();


    public class WebhooksClientConcurrentCollection
    {
        private readonly ConcurrentDictionary<ulong, DiscordWebhookClient> _webhookClients = [];

        public void Add(ulong webhookId, DiscordWebhookClient webhookClient)
        {
            Remove(webhookId);
            _webhookClients.TryAdd(webhookId, webhookClient);
        }


        public void Remove(ulong webhookId)
        {
            _webhookClients.TryRemove(webhookId, out _);
        }


        public DiscordWebhookClient? GetById(ulong webhookId)
        {
            _webhookClients.TryGetValue(webhookId, out var webhookClient);
            return webhookClient;
        }
    }


    public class SearchQueryConcurrentCollection
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
