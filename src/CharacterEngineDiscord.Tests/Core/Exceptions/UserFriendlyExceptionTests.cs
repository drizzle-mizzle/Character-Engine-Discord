using CharacterEngineDiscord.Core.Exceptions;
using FluentAssertions;

namespace CharacterEngineDiscord.Tests.Core.Exceptions;

public sealed class UserFriendlyExceptionTests
{
    [Fact]
    public void Constructor_With_Message_Should_Set_Message()
    {
        const string message = "you don't have permission to do that";

        var exception = new UserFriendlyException(message);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_With_Inner_Should_Set_InnerException()
    {
        const string message = "guild is not configured";
        var inner = new InvalidOperationException("missing row");

        var exception = new UserFriendlyException(message, inner);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Should_Be_Sealed()
    {
        typeof(UserFriendlyException).IsSealed.Should().BeTrue();
    }
}
