using System.Collections.Concurrent;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Models.Db;
using Discord;
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
    private static readonly ConcurrentDictionary<ulong, WatchedUser> _watchedUsers = [];
    private static readonly ConcurrentDictionary<ulong, object?> _blockedUsers = [];
    private static readonly ConcurrentDictionary<(ulong, ulong), object?> _blockedGuildUsers = [];

    private static readonly int WARN_THRESHOLD = BotConfig.USER_RATE_LIMIT - 2;
    private static readonly int BLOCK_THRESHOLD = BotConfig.USER_RATE_LIMIT;


    static WatchDog()
    {
        using var db = DatabaseHelper.GetDbContext();

        var blockedUsers = db.BlockedUsers.ToList();
        foreach (var user in blockedUsers)
        {
            _blockedUsers.TryAdd(user.Id, null);
        }

        var blockedGuildUsers = db.GuildBlockedUsers.ToList();
        foreach (var user in blockedGuildUsers)
        {
            _blockedGuildUsers.TryAdd((user.UserId, user.DiscordGuildId), null);
        }
    }


    public static WatchDogValidationResult ValidateUser(IGuildUser user)
    {
        if (_blockedUsers.ContainsKey(user.Id) || _blockedGuildUsers.ContainsKey((user.Id, user.Guild.Id)))
        {
            return WatchDogValidationResult.Blocked;
        }

        var watchedUser = _watchedUsers.GetOrAdd(user.Id, uid => new WatchedUser
        {
            UserId = uid,
            InteractionsCount = 0,
            LastInteractionWindowStartedAt = DateTime.Now
        });

        var validationResult = Validate(watchedUser);
        if (validationResult is WatchDogValidationResult.Blocked)
        {
            _ = BlockUserGloballyAsync(user.Id);
        }

        return validationResult;
    }


    public static async Task BlockUserGloballyAsync(ulong userId)
    {
        if (_blockedUsers.TryAdd(userId, null) == false)
        {
            return;
        }

        _watchedUsers.TryRemove(userId, out _);

        await using var db = DatabaseHelper.GetDbContext();
        await db.BlockedUsers.AddAsync(new BlockedUser
        {
            Id = userId,
            BlockedAt = DateTime.Now
        });

        await db.SaveChangesAsync();
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

        await using var db = DatabaseHelper.GetDbContext();
        var blockedUser = await db.BlockedUsers.FindAsync(userId);
        if (blockedUser is null)
        {
            return false;
        }

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


    private static WatchDogValidationResult Validate(WatchedUser user)
    {
        lock (user)
        {
            user.InteractionsCount++;
            if (user.InteractionsCount < WARN_THRESHOLD)
            {
                return WatchDogValidationResult.Passed;
            }

            var minuteHasPassed = DateTime.Now - user.LastInteractionWindowStartedAt > TimeSpan.FromMinutes(1);
            if (minuteHasPassed)
            {
                user.LastInteractionWindowStartedAt = DateTime.Now;
                user.InteractionsCount = 1;
                return WatchDogValidationResult.Passed;
            }

            if (user.InteractionsCount == WARN_THRESHOLD)
            {
                return WatchDogValidationResult.Warning;
            }

            if (user.InteractionsCount < BLOCK_THRESHOLD)
            {
                return WatchDogValidationResult.Passed;
            }
        }

        return WatchDogValidationResult.Blocked;
    }


    private record WatchedUser
    {
        public ulong UserId { get; set; }
        public int InteractionsCount { get; set; }
        public DateTime LastInteractionWindowStartedAt { get; set; }
    }

}
