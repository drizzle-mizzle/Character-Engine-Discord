namespace CharacterEngineDiscord.DiscordBot.RateLimiting;

/// <summary>
/// Outcome of a single <see cref="ICeWatchDog.Check(ulong)"/> call. Struct-typed
/// to avoid allocation on the interaction hot path.
/// </summary>
/// <param name="IsAllowed">Whether the call is allowed to proceed.</param>
/// <param name="BlockedUntil">UTC instant the block expires (when <paramref name="IsAllowed"/> is false).</param>
/// <param name="JustBlocked">
/// True only when this specific Check call flipped the user from allowed -> blocked.
/// False when the user was already blocked from a previous call. Used by callers (e.g.
/// <c>CeSlashCommandEventForwarder</c>) to fire admin notifications exactly once per ban
/// rather than on every subsequent rejected attempt.
/// </param>
// TODO: 3-stage outcome (Allowed / NearLimit / Blocked) - old WatchDog had a
// "Warning" tier where users 3 attempts away from block got a soft hint
// without being throttled. Worth restoring as UX improvement when we tune
// the rate-limit defaults - currently bot abruptly blocks at PerWindow.
public readonly record struct RateLimitDecision(bool IsAllowed, DateTime? BlockedUntil, bool JustBlocked = false);
