using CharacterEngineDiscord.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.DiscordBot.RateLimiting;

/// <summary>
/// Periodically evicts idle <see cref="CeWatchDog"/> buckets so a long-lived
/// process that has seen many distinct users does not retain unbounded state.
/// The cadence is fixed (5 min) rather than configurable: bucket cost is tiny
/// and infrequent cleanup keeps GC pressure low.
/// </summary>
internal sealed class CeWatchDogCleanupHostedService : BackgroundService
{
    // Inject the concrete CeWatchDog (not ICeWatchDog) so EvictIdle is reachable
    // without casting at runtime; both registrations resolve to the same singleton.
    private readonly CeWatchDog _watchDog;
    private readonly IOptions<RateLimitOptions> _options;
    private readonly ILogger<CeWatchDogCleanupHostedService> _logger;

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    public CeWatchDogCleanupHostedService(
        CeWatchDog watchDog,
        IOptions<RateLimitOptions> options,
        ILogger<CeWatchDogCleanupHostedService> logger)
    {
        _watchDog = watchDog;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer beats the Task.Delay loop here: it does not allocate
        // a Timer per tick and avoids the multi-second drift of repeated Task.Delay.
        using var timer = new PeriodicTimer(CleanupInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var idleThreshold = TimeSpan.FromSeconds(_options.Value.WindowSeconds * 4);
                    _watchDog.EvictIdle(idleThreshold);
                }
                catch (Exception ex)
                {
                    // Swallow inside the loop — a single failed cleanup pass should not kill
                    // the cadence; the next tick will try again.
                    _logger.LogError(ex, "WatchDog cleanup failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path — host requested stop.
        }
    }
}
