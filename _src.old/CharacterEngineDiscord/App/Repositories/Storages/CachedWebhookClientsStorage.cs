using System.Collections.Concurrent;
using System.Collections.Immutable;
using Discord.Webhook;

namespace CharacterEngine.App.Repositories.Storages;


public sealed class CachedWebhookClientsStorage
{
    private static readonly ConcurrentDictionary<ulong, CachedWebhookClient> _webhookClients = [];


    public ImmutableDictionary<ulong, CachedWebhookClient> GetAll()
        => _webhookClients.ToImmutableDictionary();


    public void Add(ulong webhookId, DiscordWebhookClient webhookClient)
    {
        Remove(webhookId);

        var newCachedClient = new CachedWebhookClient()
        {
            WebhookClient = webhookClient,
            LastHitAt = DateTime.Now
        };

        _webhookClients.TryAdd(webhookId, newCachedClient);
    }

    public void Remove(ulong webhookId)
    {
        _webhookClients.TryRemove(webhookId, out _);
    }


    public DiscordWebhookClient FindOrCreate(ulong webhookId, string webhookToken)
    {
        var cachedClient = _webhookClients.GetValueOrDefault(webhookId);

        if (cachedClient is not null)
        {
            cachedClient.LastHitAt = DateTime.Now;
            return cachedClient.WebhookClient;
        }

        var newWebhookClient = new DiscordWebhookClient(webhookId, webhookToken);
        var newCachedClient = new CachedWebhookClient()
        {
            WebhookClient = newWebhookClient,
            LastHitAt = DateTime.Now
        };

        _webhookClients.TryAdd(webhookId, newCachedClient);

        return newWebhookClient;
    }


    public static DiscordWebhookClient? Find(ulong webhookId)
        => _webhookClients.GetValueOrDefault(webhookId)?.WebhookClient;


    public class CachedWebhookClient
    {
        public required DiscordWebhookClient WebhookClient { get; init; }

        public required DateTime LastHitAt { get; set; }
    }
}
