using System.Collections.Concurrent;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Infrastructure;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Models;
using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngine.App.Services;


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

    private static ServiceProvider _serviceProvider = null!;
    private static bool _running;


    public static async Task RunAsync(ServiceProvider serviceProvider)
    {
        if (_running)
        {
            return;
        }

        _serviceProvider = serviceProvider;
        _running = true;

        await using var db = _serviceProvider.GetRequiredService<AppDbContext>();

        var blockedUsers = await db.BlockedUsers.ToArrayAsync();
        foreach (var user in blockedUsers)
        {
            _blockedUsers.TryAdd(user.Id, user.BlockedUntil);
        }

        var blockedGuildUsers = await db.GuildBlockedUsers.ToArrayAsync();
        foreach (var user in blockedGuildUsers)
        {
            _blockedGuildUsers.TryAdd((user.UserOrRoleId, user.DiscordGuildId), null);
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

        MetricsWriter.Write(MetricType.UserBlocked, userId, blockedUntil.Humanize());

        await using var db = _serviceProvider.GetRequiredService<AppDbContext>();
        db.BlockedUsers.Add(new BlockedUser
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
                    + $"\nBlocked until: **{blockedUntil.Humanize()}**";

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


    public static async Task<bool> UnblockUserGloballyAsync(ulong userId)
    {
        _blockedUsers.TryRemove(userId, out _);
        _watchedUsers.TryAdd(userId, new CachedWatchedUser(0, DateTime.Now, true));

        await using var db = _serviceProvider.GetRequiredService<AppDbContext>();

        var blockedUser = await db.BlockedUsers.FindAsync(userId);
        if (blockedUser is null)
        {
            return false;
        }

        MetricsWriter.Write(MetricType.UserUnblocked, userId);

        db.BlockedUsers.Remove(blockedUser);
        await db.SaveChangesAsync();
        return true;
    }
    
    public static async Task BlockGuildUserAsync(ulong userOrRoleId, ulong guildId, ulong blockedBy, bool isRole)
    {
        if (_blockedGuildUsers.TryAdd((userOrRoleId, guildId), null) == false)
        {
            return;
        }
    
        await using var db = _serviceProvider.GetRequiredService<AppDbContext>();

        db.GuildBlockedUsers.Add(new BlockedGuildUser
        {
            UserOrRoleId = userOrRoleId,
            DiscordGuildId = guildId,
            BlockedAt = DateTime.Now,
            BlockedBy = blockedBy,
            IsRole = isRole
        });
    
        await db.SaveChangesAsync();
    }


    public static async Task UnblockGuildUserAsync(BlockedGuildUser blockedGuildUser)
    {
        _blockedGuildUsers.TryRemove((blockedGuildUser.UserOrRoleId, blockedGuildUser.DiscordGuildId), out _);
    
        await using var db = _serviceProvider.GetRequiredService<AppDbContext>();

        db.GuildBlockedUsers.Remove(blockedGuildUser);
        await db.SaveChangesAsync();
    }
    
    public static async Task UnblockGuildUsersAsync(BlockedGuildUser[] blockedGuildUsers)
    {
        foreach (var blockedGuildUser in blockedGuildUsers)
        {
            _blockedGuildUsers.TryRemove((blockedGuildUser.UserOrRoleId, blockedGuildUser.DiscordGuildId), out _);
        }
    
        await using var db = _serviceProvider.GetRequiredService<AppDbContext>();

        db.GuildBlockedUsers.RemoveRange(blockedGuildUsers);
        
        await db.SaveChangesAsync();
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
