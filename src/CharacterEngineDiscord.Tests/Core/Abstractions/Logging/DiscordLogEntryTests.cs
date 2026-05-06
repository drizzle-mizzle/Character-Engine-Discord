using CharacterEngineDiscord.Core.Abstractions.Logging;
using FluentAssertions;

namespace CharacterEngineDiscord.Tests.Core.Abstractions.Logging;

public sealed class DiscordLogEntryTests
{
    [Fact]
    public void Default_Optional_Fields_Should_Be_Null()
    {
        var entry = new DiscordLogEntry { Title = "T" };

        entry.Title.Should().Be("T");
        entry.Message.Should().BeNull();
        entry.Exception.Should().BeNull();
        entry.TraceId.Should().BeNull();
    }

    [Fact]
    public void Records_With_Same_Values_Should_Be_Equal()
    {
        var ex = new InvalidOperationException("boom");

        var a = new DiscordLogEntry { Title = "T", Message = "m", Exception = ex, TraceId = "abc" };
        var b = new DiscordLogEntry { Title = "T", Message = "m", Exception = ex, TraceId = "abc" };

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Records_With_Different_Title_Should_Not_Be_Equal()
    {
        var a = new DiscordLogEntry { Title = "T1", Message = "m" };
        var b = new DiscordLogEntry { Title = "T2", Message = "m" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_Expression_Should_Clone_And_Override_Specified_Fields_Only()
    {
        var original = new DiscordLogEntry { Title = "T", Message = "m", TraceId = "abc" };

        var clone = original with { Title = "X" };

        clone.Title.Should().Be("X");
        clone.Message.Should().Be(original.Message);
        clone.TraceId.Should().Be(original.TraceId);
        clone.Exception.Should().BeNull();
        original.Title.Should().Be("T");
    }
}
