using System.Collections.Concurrent;
using System.Diagnostics;
using CharacterEngineDiscord.Models.Abstractions;

namespace CharacterEngine.App.Static.Entities;


public sealed class CachedCharacerInfoCollection
{
    private static readonly ConcurrentDictionary<Guid, CachedCharacterInfo> _cachedCharacters = [];


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
            WebhookId = spawnedCharacter.WebhookId,
            Conversations = new ActiveConversation(spawnedCharacter.EnableSwipes, spawnedCharacter.EnableBuffering)
        };

        _cachedCharacters.TryAdd(spawnedCharacter.Id, newCachedCharacter);
    }


    public void Remove(Guid spawnedCharacterId)
    {
        if (_cachedCharacters.ContainsKey(spawnedCharacterId))
        {
            _cachedCharacters.TryRemove(spawnedCharacterId, out _);
        }
    }


    public CachedCharacterInfo? GetByWebhookId(ulong webhookId)
        => _cachedCharacters.FirstOrDefault(c => c.Value.WebhookId == webhookId).Value;


    public CachedCharacterInfo? Find(string callPrefixOrIdOrWebhookId, ulong channelId)
    {
        var cachedCharacter = _cachedCharacters.FirstOrDefault(c => c.Value.CallPrefix == callPrefixOrIdOrWebhookId && c.Value.ChannelId == channelId);

        if (cachedCharacter.Key == default && Guid.TryParse(callPrefixOrIdOrWebhookId, out var characterId))
        {
            cachedCharacter = _cachedCharacters.FirstOrDefault(c => c.Value.Id == characterId && c.Value.ChannelId == channelId);
        }

        if (cachedCharacter.Key == default && ulong.TryParse(callPrefixOrIdOrWebhookId, out var webhookId))
        {
            cachedCharacter = _cachedCharacters.FirstOrDefault(c => c.Value.WebhookId == webhookId && c.Value.ChannelId == channelId);
        }

        return cachedCharacter.Key == default ? null : cachedCharacter.Value;
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

    public required ActiveConversation Conversations { get; init; }
}


public record ActiveConversation
{
    public ActiveConversation(bool swipable, bool useBuffer)
    {
        if (swipable)
        {
            SwipableCharacterMessages = [];
        }

        if (useBuffer)
        {
            BufferTimer = new Stopwatch();
            BufferedUserMessages = [];
        }
    }

    public ulong LastCharacterDiscordMessageId { get; set; } = 0;
    public bool WritingResponse { get; set; } = false;
    public DateTime LastActive { get; set; } = DateTime.Now;

    public int SelectedSwipableMessageIndex { get; set; } = 0;
    public List<CharacterMessage>? SwipableCharacterMessages { get; }

    public Stopwatch? BufferTimer { get; }
    public List<UserMessage>? BufferedUserMessages { get; }
}


public record UserMessage
{
    public required ulong MessageId { get; init; }

    public required string Content { get; init; }
}


public record CharacterMessage
{
    public required string MessageId { get; init; }
    public required string Content { get; init; }
    public string? ImageUrl { get; set; }
}
