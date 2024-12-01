using System.Collections.Concurrent;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Models.Db;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace CharacterEngine.App.Static;


public enum WatchDogValidationResult
{
    Passed,
    Warning,
    Blocked
}


public static class WatchDog
{
    private static readonly ConcurrentDictionary<ulong, CachedWatchedUser> _watchedUsers = [];
    private static readonly ConcurrentDictionary<ulong, DateTime> _blockedUsers = [];
    private static readonly ConcurrentDictionary<(ulong UserId, ulong GuildId), object?> _blockedGuildUsers = [];

    private static readonly int WARN_THRESHOLD = BotConfig.USER_RATE_LIMIT - 3;
    private static readonly int BLOCK_THRESHOLD = BotConfig.USER_RATE_LIMIT;


    public static async Task RunAsync()
    {
        await using var db = DatabaseHelper.GetDbContext();

        var blockedUsers = await db.BlockedUsers.ToArrayAsync();
        foreach (var user in blockedUsers)
        {
            _blockedUsers.TryAdd(user.Id, user.BlockedUntil);
        }

        var blockedGuildUsers = await db.GuildBlockedUsers.ToArrayAsync();
        foreach (var user in blockedGuildUsers)
        {
            _blockedGuildUsers.TryAdd((user.UserId, user.DiscordGuildId), null);
        }
    }


    public static (WatchDogValidationResult Result, DateTime? BlockedUntil) ValidateUser(IGuildUser user, ITextChannel? channel, bool justCheck = false)
    {
        if (_blockedUsers.ContainsKey(user.Id) || _blockedGuildUsers.ContainsKey((user.Id, user.GuildId)))
        {
            return (WatchDogValidationResult.Blocked, null);
        }

        if (justCheck)
        {
            return (WatchDogValidationResult.Passed, null);
        }

        var watchedUser = _watchedUsers.GetOrAdd(user.Id, _ => new CachedWatchedUser(0, DateTime.Now, false));

        var validation = Validate(watchedUser);
        if (validation.Result is WatchDogValidationResult.Blocked)
        {
            _ = BlockUserGloballyAsync(user.Id, channel, (DateTime)validation.BlockedUntil!);
        }

        return validation;
    }


    public static async Task BlockUserGloballyAsync(ulong userId, ITextChannel? channel, DateTime blockedUntil)
    {
        if (_blockedUsers.TryAdd(userId, blockedUntil) == false)
        {
            return;
        }

        _watchedUsers.TryRemove(userId, out _);

        MetricsWriter.Create(MetricType.UserBlocked, userId, blockedUntil.HumanizeDateTime());

        await using var db = DatabaseHelper.GetDbContext();
        await db.BlockedUsers.AddAsync(new BlockedUser
        {
            Id = userId,
            BlockedAt = DateTime.Now,
            BlockedUntil = blockedUntil
        });

        await db.SaveChangesAsync();

        var user = CharacterEngineBot.DiscordClient.GetUser(userId);
        var message = "**User blocked**\n" + (user is null
                    ? $"User: **{userId}**"
                    : $"User: **{user.Username}** ({userId}) {(user.IsBot ? "(bot)" : user.IsWebhook ? "(webhook)" : "")}")
                    + $"\nBlocked until: **{blockedUntil.HumanizeDateTime()}**";

        string? content = null;
        if (channel is not null)
        {
            var last20Messages = await channel.GetMessagesAsync(20).FlattenAsync();
            content = string.Join("\n", last20Messages.Select(FormattedMessages));
        }

        await CharacterEngineBot.DiscordClient.ReportLogAsync(message, content);
    }


    private static string FormattedMessages(IMessage message)
    {
        var result = $"[{message.Id}] ({(message.Author.IsBot ? "bot" : message.Author.IsWebhook ? "webhook" : "user")}) **{message.Author.Username}**: {message.Content}";
        if (message.Reference is MessageReference messageReference)
        {
            result = $"[to: {messageReference.MessageId}] {result}";
        }

        return result;
    }


    public static async Task BlockGuildUserAsync(IGuildUser user, ulong adminId)
    {
        if (_blockedGuildUsers.TryAdd((user.Id, user.GuildId), null) == false)
        {
            return;
        }

        await using var db = DatabaseHelper.GetDbContext();
        await db.GuildBlockedUsers.AddAsync(new BlockedGuildUser
        {
            UserId = user.Id,
            DiscordGuildId = user.GuildId,
            BlockedBy = adminId,
            BlockedAt = DateTime.Now
        });

        await db.SaveChangesAsync();
    }


    public static async Task<bool> UnblockUserGloballyAsync(ulong userId)
    {
        _blockedUsers.TryRemove(userId, out _);
        _watchedUsers.TryAdd(userId, new CachedWatchedUser(0, DateTime.Now, true));

        await using var db = DatabaseHelper.GetDbContext();
        var blockedUser = await db.BlockedUsers.FindAsync(userId);
        if (blockedUser is null)
        {
            return false;
        }


        MetricsWriter.Create(MetricType.UserUnblocked, userId);

        db.BlockedUsers.Remove(blockedUser);
        await db.SaveChangesAsync();
        return true;
    }


    public static async Task<bool> UnblockGuildUserAsync(IGuildUser user)
    {
        _blockedGuildUsers.TryRemove((user.Id, user.GuildId), out _);

        await using var db = DatabaseHelper.GetDbContext();
        var blockedGuildUser = await db.GuildBlockedUsers.FirstOrDefaultAsync(u => u.UserId == user.Id && u.DiscordGuildId == user.GuildId);
        if (blockedGuildUser is null)
        {
            return false;
        }

        db.GuildBlockedUsers.Remove(blockedGuildUser);
        await db.SaveChangesAsync();

        return true;
    }


    private static (WatchDogValidationResult Result, DateTime? BlockedUntil) Validate(CachedWatchedUser user)
    {
        lock (user)
        {
            user.InteractionsCount++;
            if (user.InteractionsCount < WARN_THRESHOLD)
            {
                return (WatchDogValidationResult.Passed, null);
            }

            var sec30HasPassed = (DateTime.Now - user.LastInteractionWindowStartedAt) > TimeSpan.FromSeconds(30);
            if (sec30HasPassed)
            {
                user.LastInteractionWindowStartedAt = DateTime.Now;
                user.InteractionsCount = 1;
                return (WatchDogValidationResult.Passed, null);
            }

            if (user.InteractionsCount == WARN_THRESHOLD)
            {
                return (WatchDogValidationResult.Warning, null);
            }

            if (user.InteractionsCount < BLOCK_THRESHOLD)
            {
                return (WatchDogValidationResult.Warning, null);
            }
        }

        var blockedUntil = user.WasBlockedBefore ? DateTime.Now.AddMinutes(BotConfig.USER_FIRST_BLOCK_MINUTES) : DateTime.Now.AddHours(BotConfig.USER_SECOND_BLOCK_HOURS);
        return (WatchDogValidationResult.Blocked, blockedUntil);
    }


    private class CachedWatchedUser(int InteractionsCount, DateTime LastInteractionWindowStartedAt, bool WasBlockedBefore)
    {
        public int InteractionsCount { get; set; } = InteractionsCount;
        public DateTime LastInteractionWindowStartedAt { get; set; } = LastInteractionWindowStartedAt;
        public bool WasBlockedBefore { get; } = WasBlockedBefore;
    }
}
