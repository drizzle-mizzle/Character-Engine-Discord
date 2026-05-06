using System.ComponentModel.DataAnnotations;
using CharacterEngineDiscord.Core.Configuration;
using FluentAssertions;

namespace CharacterEngineDiscord.Tests.Core.Configuration;

public sealed class BotOptionsAnnotationsTests
{
    [Fact]
    public void TryValidateObject_Should_Fail_When_Token_Is_Empty()
    {
        var options = new BotOptions { Token = string.Empty };
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();

        var ok = Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);

        ok.Should().BeFalse();
        results.Should().NotBeEmpty();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(BotOptions.Token)));
    }

    [Fact]
    public void TryValidateObject_Should_Succeed_When_Token_Is_Provided()
    {
        var options = new BotOptions { Token = "abc" };
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();

        var ok = Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);

        ok.Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Fact]
    public void TryValidateObject_Should_Allow_PlayingStatus_To_Be_Null()
    {
        var options = new BotOptions { Token = "abc", PlayingStatus = null };
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();

        var ok = Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);

        ok.Should().BeTrue();
        results.Should().BeEmpty();
    }
}
