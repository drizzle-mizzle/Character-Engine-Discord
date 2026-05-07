using CharacterEngineDiscord.Core.Abstractions.Time;
using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.DiscordBot.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CharacterEngineDiscord.Tests.DiscordBot.RateLimiting;

public sealed class CeWatchDogTests
{
    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public void Advance(TimeSpan ts) => UtcNow += ts;
    }

    private static IOptions<RateLimitOptions> RateOptions(
        int perWindow = 3,
        int windowSec = 30,
        int firstBlockMin = 5,
        int secondBlockHr = 1)
        => Options.Create(new RateLimitOptions
        {
            PerWindow = perWindow,
            WindowSeconds = windowSec,
            FirstBlockMinutes = firstBlockMin,
            SecondBlockHours = secondBlockHr,
        });

    private static IOptions<AdminOptions> AdminOptionsWith(params ulong[] owners)
        => Options.Create(new AdminOptions
        {
            GuildId = 1,
            LogsChannelId = 2,
            ErrorsChannelId = 3,
            OwnerUserIds = owners,
        });

    // --------- 4.8.2 Behaviour tests ---------

    [Fact]
    public void Check_FirstAttempt_Should_Be_Allowed()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(), AdminOptionsWith(), clock);

        var decision = dog.Check(userId: 100);

        decision.IsAllowed.Should().BeTrue();
        decision.BlockedUntil.Should().BeNull();
    }

    [Fact]
    public void Check_BelowLimit_Should_Allow_All()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(perWindow: 3), AdminOptionsWith(), clock);

        var d1 = dog.Check(100);
        var d2 = dog.Check(100);
        var d3 = dog.Check(100);

        d1.IsAllowed.Should().BeTrue();
        d2.IsAllowed.Should().BeTrue();
        d3.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Check_AboveLimit_Should_Block_Subsequent()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(perWindow: 3), AdminOptionsWith(), clock);

        dog.Check(100);
        dog.Check(100);
        dog.Check(100);
        var blocked = dog.Check(100);

        blocked.IsAllowed.Should().BeFalse();
        blocked.BlockedUntil.Should().NotBeNull();
        // The transition allowed -> blocked must surface JustBlocked = true so the forwarder
        // can fire exactly one admin-channel notification per ban.
        blocked.JustBlocked.Should().BeTrue();
    }

    [Fact]
    public void Check_AlreadyBlocked_Should_Not_Mark_JustBlocked()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(perWindow: 3), AdminOptionsWith(), clock);

        // Trip the block on the 4th call.
        dog.Check(100);
        dog.Check(100);
        dog.Check(100);
        var firstBlock = dog.Check(100);

        // Subsequent attempts while still blocked must NOT re-mark JustBlocked.
        var secondAttempt = dog.Check(100);
        var thirdAttempt = dog.Check(100);

        firstBlock.IsAllowed.Should().BeFalse();
        firstBlock.JustBlocked.Should().BeTrue();

        secondAttempt.IsAllowed.Should().BeFalse();
        secondAttempt.JustBlocked.Should().BeFalse();
        thirdAttempt.IsAllowed.Should().BeFalse();
        thirdAttempt.JustBlocked.Should().BeFalse();
    }

    [Fact]
    public void Check_AllowedAttempts_Should_Not_Mark_JustBlocked()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(perWindow: 3), AdminOptionsWith(), clock);

        var d1 = dog.Check(100);
        var d2 = dog.Check(100);

        // Allowed paths must always have JustBlocked = false; the flag is reserved for the
        // single transition allowed -> blocked.
        d1.IsAllowed.Should().BeTrue();
        d1.JustBlocked.Should().BeFalse();
        d2.IsAllowed.Should().BeTrue();
        d2.JustBlocked.Should().BeFalse();
    }

    [Fact]
    public void Check_AfterWindow_Should_Allow_Again()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(perWindow: 3, windowSec: 30), AdminOptionsWith(), clock);

        dog.Check(100);
        dog.Check(100);
        dog.Check(100);

        clock.Advance(TimeSpan.FromSeconds(31));

        var decision = dog.Check(100);

        decision.IsAllowed.Should().BeTrue();
        decision.BlockedUntil.Should().BeNull();
    }

    [Fact]
    public void Check_BlockedUntil_Should_Reflect_FirstBlockMinutes_For_First_Block()
    {
        var clock = new TestClock();
        var startUtc = clock.UtcNow;
        var dog = new CeWatchDog(RateOptions(perWindow: 3, firstBlockMin: 5), AdminOptionsWith(), clock);

        dog.Check(100);
        dog.Check(100);
        dog.Check(100);
        var blocked = dog.Check(100);

        blocked.IsAllowed.Should().BeFalse();
        blocked.BlockedUntil.Should().Be(startUtc.AddMinutes(5));
    }

    [Fact]
    public void Check_AfterFirstBlockExpires_Should_Allow_Again()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(perWindow: 3, firstBlockMin: 5), AdminOptionsWith(), clock);

        dog.Check(100);
        dog.Check(100);
        dog.Check(100);
        dog.Check(100); // -> blocked

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        var decision = dog.Check(100);

        decision.IsAllowed.Should().BeTrue();
        decision.BlockedUntil.Should().BeNull();
    }

    [Fact]
    public void Check_SecondBlock_Should_Use_SecondBlockHours()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(perWindow: 3, firstBlockMin: 5, secondBlockHr: 1), AdminOptionsWith(), clock);

        // First overrun -> 5 min block.
        dog.Check(100);
        dog.Check(100);
        dog.Check(100);
        dog.Check(100);

        // Wait out the first block.
        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        // Second overrun should escalate to the long block (1 hour).
        var afterExpire = dog.Check(100); // first allowed after expiry
        afterExpire.IsAllowed.Should().BeTrue();

        dog.Check(100);
        dog.Check(100);
        var secondBlock = dog.Check(100);

        secondBlock.IsAllowed.Should().BeFalse();
        secondBlock.BlockedUntil.Should().NotBeNull();
        var elapsedFromNow = secondBlock.BlockedUntil!.Value - clock.UtcNow;
        elapsedFromNow.Should().BeCloseTo(TimeSpan.FromHours(1), precision: TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Check_OwnerInList_Should_Skip_Limits()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(perWindow: 3), AdminOptionsWith(owners: 42), clock);

        for (var i = 0; i < 100; i++)
        {
            var decision = dog.Check(42);
            decision.IsAllowed.Should().BeTrue($"owner attempt {i} must be allowed");
            decision.BlockedUntil.Should().BeNull();
        }
    }

    [Fact]
    public void Check_DifferentUsers_Have_Independent_Buckets()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(perWindow: 3), AdminOptionsWith(), clock);

        // userA exhausts.
        dog.Check(100);
        dog.Check(100);
        dog.Check(100);
        var aBlocked = dog.Check(100);

        // userB still fresh.
        var bFirst = dog.Check(200);

        aBlocked.IsAllowed.Should().BeFalse();
        bFirst.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Check_Concurrency_Should_Be_Threadsafe()
    {
        var clock = new TestClock();
        // Window large enough that no entry ever ages out during the test, so we measure
        // the limiter's pure threshold behaviour, not the prune logic.
        var dog = new CeWatchDog(RateOptions(perWindow: 50, windowSec: 600), AdminOptionsWith(), clock);

        var allowed = 0;
        Parallel.For(0, 1000, _ =>
        {
            if (dog.Check(7).IsAllowed)
            {
                Interlocked.Increment(ref allowed);
            }
        });

        // Exactly PerWindow attempts can succeed before the first block trips. The
        // remainder are either rejected by the threshold check or blocked.
        allowed.Should().Be(50);
    }

    // --------- 4.8.3 EvictIdle tests ---------

    [Fact]
    public void EvictIdle_Should_Remove_Idle_Buckets()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(), AdminOptionsWith(), clock);

        dog.Check(1);
        clock.Advance(TimeSpan.FromMinutes(10));

        dog.EvictIdle(TimeSpan.FromMinutes(2));

        dog.Buckets.Should().NotContainKey(1ul);
    }

    [Fact]
    public void EvictIdle_Should_Keep_Active_Buckets()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(), AdminOptionsWith(), clock);

        dog.Check(1);
        clock.Advance(TimeSpan.FromSeconds(30));

        dog.EvictIdle(TimeSpan.FromMinutes(2));

        dog.Buckets.Should().ContainKey(1ul);
    }

    [Fact]
    public void EvictIdle_Should_Keep_Blocked_Buckets_Even_If_LastTouched_Old()
    {
        var clock = new TestClock();
        var dog = new CeWatchDog(RateOptions(perWindow: 1, firstBlockMin: 60), AdminOptionsWith(), clock);

        dog.Check(1); // allow
        dog.Check(1); // exceed -> blocked

        clock.Advance(TimeSpan.FromMinutes(10));

        dog.EvictIdle(TimeSpan.FromMinutes(2));

        dog.Buckets.Should().ContainKey(1ul);
    }
}
