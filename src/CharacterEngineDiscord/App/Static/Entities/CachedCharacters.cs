using System.Collections.Concurrent;
using CharacterEngine.App.Helpers;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.Static.Entities;


public sealed class CachedCharacerInfoCollection
{
    private readonly ConcurrentDictionary<Guid, CachedCharacterInfo> _cachedCharacters = [];


    public async Task AddRangeAsync(ICollection<ISpawnedCharacter> spawnedCharacters)
    {
        await using var db = DatabaseHelper.GetDbContext();
        var allHuntedUsers = await db.HuntedUsers.ToArrayAsync();

        Parallel.ForEach(spawnedCharacters, AddNew);

        return;

        void AddNew(ISpawnedCharacter sc)
        {
            var huntedUsersIds = allHuntedUsers.Where(hu => hu.SpawnedCharacterId == sc.Id)
                                               .Select(hu => hu.DiscordUserId)
                                               .ToList();
            Add(sc, huntedUsersIds);
        }
    }


    public CachedCharacterInfo Add(ISpawnedCharacter spawnedCharacter, List<ulong> huntedUserIds)
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
            HuntedUsers = huntedUserIds
            // CachedUserMessages = new CachedUserMessages(),
            // Conversations = new ActiveConversation(spawnedCharacter.EnableSwipes)
        };

        _cachedCharacters.TryAdd(spawnedCharacter.Id, newCachedCharacter);
        return newCachedCharacter;
    }


    public void Remove(Guid spawnedCharacterId)
    {
        _cachedCharacters.TryRemove(spawnedCharacterId, out _);
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
    private readonly List<ulong> _queue = [];

    public bool QueueIsFullFor(ulong userId)
    {
        lock (_queue)
        {
            return _queue.Count >= 5 || _queue.Count(id => id == userId) > 1;
        }
    }


    public bool QueueIsTurnOf(ulong userId)
    {
        lock (_queue)
        {
            return _queue.Count < 2 || _queue[0] == userId;
        }
    }


    public void QueueAdd(ulong userId)
    {
        lock (_queue)
        {
            _queue.Add(userId);
        }
    }

    public void QueueRemove(ulong userId)
    {
        lock (_queue)
        {
            _queue.Remove(userId);
        }
    }


    public required Guid Id { get; init; }
    public required ulong ChannelId { get; init; }
    public required string WebhookId { get; init; }
    public required IntegrationType IntegrationType { get; init; }

    public required string CallPrefix { get; set; }
    public required double FreewillFactor { get; set; }
    public ulong? WideContextLastMessageId { get; set; }

    public required List<ulong> HuntedUsers { get; set; }

    // public required CachedUserMessages CachedUserMessages { get; init; }
    // public required ActiveConversation Conversations { get; init; }
}


public record ActiveConversation
{
    public ActiveConversation(bool swipable)
    {
        if (swipable)
        {
            SwipableCharacterMessages = [];
        }
    }

    public ulong LastCharacterDiscordMessageId { get; set; } = 0;
    public bool WritingResponse { get; set; } = false;
    public DateTime LastActive { get; set; } = DateTime.Now;

    public int SelectedSwipableMessageIndex { get; set; } = 0;
    public List<CharacterMessage>? SwipableCharacterMessages { get; }
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


public sealed class CachedUserMessages
{
    private readonly ConcurrentDictionary<ulong, CachedUserMessage> _cachedUserMessages = [];


    public void AddRange(IEnumerable<IUserMessage> messages)
    {
        Parallel.ForEach(messages, Add);
    }


    public void Add(IUserMessage message)
    {
        Remove(message.Id);

        var author = (SocketGuildUser)message.Author;
        var newCachedMessage = new CachedUserMessage
        {
            MessageId = message.Id,
            UserId = author.Id,
            Username = author.Username,
            UserMention = author.Mention,
            CreatedAt = message.CreatedAt.LocalDateTime
        };

        _cachedUserMessages.TryAdd(message.Id, newCachedMessage);
    }


    public void Remove(ulong messageId)
    {
        _cachedUserMessages.TryRemove(messageId, out _);
    }
}


public class CachedUserMessage
{
    public required ulong MessageId { get; set; }

    public required ulong UserId { get; set; }
    public required string Username { get; set; }
    public required string UserMention { get; set; }
    public required DateTime CreatedAt { get; set; }
}
