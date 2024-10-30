using System.Collections.Concurrent;
using CharacterEngineDiscord.Models.Abstractions;
using Discord.Webhook;

namespace CharacterEngine.App.Static.Entities;


public sealed class CachedWebhookClientCollection
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


    public DiscordWebhookClient FindOrCreate(ISpawnedCharacter spawnedCharacter)
    {
        var webhookClient = _webhookClients.GetValueOrDefault(spawnedCharacter.WebhookId);

        if (webhookClient is null)
        {
            webhookClient = new DiscordWebhookClient(spawnedCharacter.WebhookId, spawnedCharacter.WebhookToken);
            Add(spawnedCharacter.WebhookId, webhookClient);
        }

        return webhookClient;
    }


    public DiscordWebhookClient? Find(ulong webhookId)
        => _webhookClients.GetValueOrDefault(webhookId);
}
