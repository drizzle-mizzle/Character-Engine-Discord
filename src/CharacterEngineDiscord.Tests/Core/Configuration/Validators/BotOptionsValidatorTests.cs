using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.Core.Configuration.Validators;
using FluentAssertions;

namespace CharacterEngineDiscord.Tests.Core.Configuration.Validators;

public sealed class BotOptionsValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Validate_Should_Fail_When_Token_Is_Empty_Or_Whitespace(string token)
    {
        var sut = new BotOptionsValidator();
        var options = new BotOptions { Token = token };

        var result = sut.Validate(name: null, options: options);

        result.Succeeded.Should().BeFalse();
        result.Failed.Should().BeTrue();
        result.Failures.Should().NotBeNullOrEmpty();
        result.Failures.Should().Contain(f => f.Contains(nameof(BotOptions.Token)));
    }

    [Fact]
    public void Validate_Should_Fail_When_Token_Is_Null()
    {
        var sut = new BotOptionsValidator();
        var options = new BotOptions { Token = null! };

        var result = sut.Validate(name: null, options: options);

        result.Succeeded.Should().BeFalse();
        result.Failed.Should().BeTrue();
        result.Failures.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_Should_Succeed_When_Token_Is_Provided()
    {
        var sut = new BotOptionsValidator();
        var options = new BotOptions { Token = "abc" };

        var result = sut.Validate(name: null, options: options);

        result.Succeeded.Should().BeTrue();
        result.Failed.Should().BeFalse();
    }
}
