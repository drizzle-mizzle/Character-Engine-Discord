using System.Collections.Concurrent;
using System.Diagnostics;
using CharacterEngine.App.Helpers;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;

namespace CharacterEngine.App.Static.Entities;


public sealed class CachedCharacerInfoCollection
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
            WebhookId = spawnedCharacter.WebhookId.ToString(),
            IntegrationType = spawnedCharacter.GetIntegrationType(),
            FreewillFactor = spawnedCharacter.FreewillFactor,
            Conversations = new ActiveConversation(spawnedCharacter.EnableSwipes, spawnedCharacter.EnableWideContext)
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


    public CachedCharacterInfo? Find(string callPrefixOrWebhookId, ulong channelId)
        => _cachedCharacters.Values.FirstOrDefault(c => c.ChannelId == channelId && (c.CallPrefix == callPrefixOrWebhookId || c.WebhookId == callPrefixOrWebhookId));


    public CachedCharacterInfo? Find(Guid Id)
    {
        _cachedCharacters.TryGetValue(Id, out var cachedCharacter);
        return cachedCharacter;
    }


    public List<CachedCharacterInfo> ToList()
        => _cachedCharacters.Values.ToList();

    public List<CachedCharacterInfo> ToList(ulong channelId)
        => _cachedCharacters.Values.Where(c => c.ChannelId == channelId).ToList();
}


public record CachedCharacterInfo
{
    public required Guid Id { get; init; }
    public required ulong ChannelId { get; init; }
    public required string CallPrefix { get; init; }
    public required string WebhookId { get; init; }
    public required IntegrationType IntegrationType { get; init; }

    public required double FreewillFactor { get; set; }

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
