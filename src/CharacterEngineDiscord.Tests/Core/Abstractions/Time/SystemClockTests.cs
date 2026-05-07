using CharacterEngineDiscord.Core.Abstractions.Time;
using FluentAssertions;

namespace CharacterEngineDiscord.Tests.Core.Abstractions.Time;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_Should_Be_Close_To_DateTime_UtcNow()
    {
        var clock = new SystemClock();

        var before = DateTime.UtcNow;
        var clockTime = clock.UtcNow;
        var after = DateTime.UtcNow;

        clockTime.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
