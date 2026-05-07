using System.Collections.Concurrent;
using CharacterEngineDiscord.Core.Abstractions.Time;
using CharacterEngineDiscord.Core.Configuration;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.DiscordBot.RateLimiting;

/// <summary>
/// In-memory per-user rate limiter. Designed for interaction hot path:
/// stationary <c>Check</c> performs one lock-free <see cref="ConcurrentDictionary{TKey,TValue}.TryGetValue"/>
/// (via <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, System.Func{TKey, TValue})"/>) plus
/// one short critical section on the per-user bucket. Owner-listed users skip the dictionary entirely.
/// State is process-local; restarts reset all counters by design.
/// </summary>
internal sealed class CeWatchDog : ICeWatchDog
{
    // Snapshot of options at construction. The hot path is sized for tens-to-hundreds of
    // interactions/sec across thousands of users; re-resolving IOptions on every Check
    // would add an indirection per call for no practical gain (these knobs are operator-tuned,
    // not user-tuned, and a restart on change is acceptable).
    private readonly RateLimitOptions _options;
    private readonly AdminOptions _adminOptions;
    private readonly IClock _clock;

    // TODO: Persist blocks across bot restarts.
    // Current behaviour: in-memory only - restart wipes all blocks (regression vs old WatchDog
    // which had a BlockedUser DB table loaded on startup). To restore parity, add a
    // `blocked_users` table in DataAccess (Id, BlockedUntil, BlockedAt, BlockCount), load
    // active rows in `CeWatchDogBootstrapHostedService.StartAsync` (new) into _buckets,
    // and persist via the message bus when JustBlocked transitions occur (so .Server owns
    // the DB write, .DiscordBot only signals).
    private readonly ConcurrentDictionary<ulong, UserBucket> _buckets = new();

    public CeWatchDog(
        IOptions<RateLimitOptions> options,
        IOptions<AdminOptions> adminOptions,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(adminOptions);
        ArgumentNullException.ThrowIfNull(clock);

        _options = options.Value;
        _adminOptions = adminOptions.Value;
        _clock = clock;
    }

    public RateLimitDecision Check(ulong userId)
    {
        // Owner-skip — fast path, no dictionary touch.
        if (IsOwner(userId))
        {
            return new RateLimitDecision(IsAllowed: true, BlockedUntil: null, JustBlocked: false);
        }

        // The factory delegate is intentionally `static` — capturing nothing means no
        // closure object is allocated per call site.
        var bucket = _buckets.GetOrAdd(userId, static _ => new UserBucket());
        return bucket.TryAcquire(_clock.UtcNow, _options);
    }

    /// <summary>
    /// Removes idle buckets to prevent unbounded memory growth in long-running processes.
    /// Called periodically by <see cref="CeWatchDogCleanupHostedService"/>; never call from
    /// the interaction hot path.
    /// </summary>
    internal void EvictIdle(TimeSpan idleThreshold)
    {
        var cutoff = _clock.UtcNow - idleThreshold;
        foreach (var kv in _buckets)
        {
            if (kv.Value.IsIdleSince(cutoff))
            {
                _buckets.TryRemove(kv.Key, out _);
            }
        }
    }

    /// <summary>
    /// Read-only view over the live bucket map for cleanup and tests. Do NOT mutate via this view.
    /// </summary>
    internal IReadOnlyDictionary<ulong, UserBucket> Buckets => _buckets;

    private bool IsOwner(ulong userId)
    {
        // Manual loop, not LINQ Contains — owner list is N <= ~5 in practice and the
        // LINQ overload allocates an enumerator on the heap.
        var owners = _adminOptions.OwnerUserIds;
        for (var i = 0; i < owners.Length; i++)
        {
            if (owners[i] == userId)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Per-user state. All access is synchronized on the per-bucket <c>_lock</c> to keep
    /// contention narrow and avoid a single global lock across the bot.
    /// </summary>
    internal sealed class UserBucket
    {
        private readonly object _lock = new();

        // Capacity 16 is comfortably above the expected steady-state PerWindow (default 15)
        // so the queue's backing array does not grow once warm.
        private readonly Queue<DateTime> _recentAttempts = new(capacity: 16);

        private DateTime? _blockedUntil;
        private int _blockCount;
        private DateTime _lastTouchedAt;

        public RateLimitDecision TryAcquire(DateTime now, RateLimitOptions options)
        {
            lock (_lock)
            {
                // Block check: if a block is still active, reject and tell the caller when it ends.
                // JustBlocked is false here - the user was already blocked by an earlier call,
                // so callers must NOT re-fire admin notifications.
                if (_blockedUntil.HasValue && _blockedUntil.Value > now)
                {
                    return new RateLimitDecision(IsAllowed: false, BlockedUntil: _blockedUntil, JustBlocked: false);
                }

                // Block expired — clear state and let the user back in. _blockCount is intentionally
                // NOT reset here so a repeat offender escalates straight to the long block.
                if (_blockedUntil.HasValue && _blockedUntil.Value <= now)
                {
                    _blockedUntil = null;
                    _recentAttempts.Clear();
                }

                // Slide the window forward by dropping timestamps older than (now - WindowSeconds).
                var cutoff = now - TimeSpan.FromSeconds(options.WindowSeconds);
                while (_recentAttempts.Count > 0 && _recentAttempts.Peek() < cutoff)
                {
                    _recentAttempts.Dequeue();
                }

                // Threshold: at-or-above PerWindow within the window means trip the block.
                // JustBlocked is true here - this is the transition allowed -> blocked, so the
                // forwarder should publish exactly one admin notification.
                if (_recentAttempts.Count >= options.PerWindow)
                {
                    _blockCount++;
                    _blockedUntil = now + EscalateBlock(_blockCount, options);
                    _recentAttempts.Clear();
                    _lastTouchedAt = now;
                    return new RateLimitDecision(IsAllowed: false, BlockedUntil: _blockedUntil, JustBlocked: true);
                }

                // Acquire: record the attempt and allow.
                _recentAttempts.Enqueue(now);
                _lastTouchedAt = now;
                return new RateLimitDecision(IsAllowed: true, BlockedUntil: null, JustBlocked: false);
            }
        }

        public bool IsIdleSince(DateTime cutoff)
        {
            // Hold the lock so we read a consistent (block-state, last-touched) pair. A blocked
            // bucket must NEVER be evicted, regardless of how stale _lastTouchedAt looks, otherwise
            // the user's escalating block count would be lost on cleanup.
            lock (_lock)
            {
                return _blockedUntil is null && _lastTouchedAt < cutoff;
            }
        }

        private static TimeSpan EscalateBlock(int blockCount, RateLimitOptions options)
        {
            return blockCount switch
            {
                1 => TimeSpan.FromMinutes(options.FirstBlockMinutes),
                _ => TimeSpan.FromHours(options.SecondBlockHours),
            };
        }
    }
}
