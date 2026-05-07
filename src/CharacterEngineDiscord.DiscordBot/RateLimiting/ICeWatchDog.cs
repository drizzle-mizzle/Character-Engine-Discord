namespace CharacterEngineDiscord.DiscordBot.RateLimiting;

/// <summary>
/// Per-user rate limiter for Discord interactions. Synchronous to keep the
/// interaction hot path fast (no Task allocation, no async state machine).
/// Owner-listed user ids are bypassed.
/// </summary>
public interface ICeWatchDog
{
    RateLimitDecision Check(ulong userId);
}
