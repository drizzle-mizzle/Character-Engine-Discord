using System.Collections.Concurrent;
using Discord.Webhook;

namespace CharacterEngine.App.Repositories.Storages;


public sealed class CachedWebhookClientsStorage
{
    private static readonly ConcurrentDictionary<ulong, DiscordWebhookClient> _webhookClients = [];


    public void Add(ulong webhookId, DiscordWebhookClient webhookClient)
    {
        Remove(webhookId);
        _webhookClients.TryAdd(webhookId, webhookClient);
    }

    public void Remove(ulong webhookId)
    {
        _webhookClients.TryRemove(webhookId, out _);
    }


    public DiscordWebhookClient FindOrCreate(ulong webhookId, string webhookToken)
    {
        var webhookClient = _webhookClients.GetValueOrDefault(webhookId);

        if (webhookClient is null)
        {
            webhookClient = new DiscordWebhookClient(webhookId, webhookToken);
            Add(webhookId, webhookClient);
        }

        return webhookClient;
    }


    public DiscordWebhookClient? Find(ulong webhookId)
        => _webhookClients.GetValueOrDefault(webhookId);
}
