using CharacterEngineDiscord.Core.Configuration;
using CharacterEngineDiscord.Core.Configuration.Validators;
using FluentAssertions;

namespace CharacterEngineDiscord.Tests.Core.Configuration.Validators;

public sealed class AdminOptionsValidatorTests
{
    private static AdminOptions Build(
        ulong guildId = 1ul,
        ulong logsChannelId = 1ul,
        ulong errorsChannelId = 1ul,
        ulong[]? ownerUserIds = null) => new()
    {
        GuildId = guildId,
        LogsChannelId = logsChannelId,
        ErrorsChannelId = errorsChannelId,
        OwnerUserIds = ownerUserIds ?? [1ul],
    };

    [Fact]
    public void Validate_Should_Fail_When_GuildId_Is_Zero()
    {
        var sut = new AdminOptionsValidator();
        var options = Build(guildId: 0ul);

        var result = sut.Validate(name: null, options: options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains(nameof(AdminOptions.GuildId)));
    }

    [Fact]
    public void Validate_Should_Fail_When_LogsChannelId_Is_Zero()
    {
        var sut = new AdminOptionsValidator();
        var options = Build(logsChannelId: 0ul);

        var result = sut.Validate(name: null, options: options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains(nameof(AdminOptions.LogsChannelId)));
    }

    [Fact]
    public void Validate_Should_Fail_When_ErrorsChannelId_Is_Zero()
    {
        var sut = new AdminOptionsValidator();
        var options = Build(errorsChannelId: 0ul);

        var result = sut.Validate(name: null, options: options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains(nameof(AdminOptions.ErrorsChannelId)));
    }

    [Fact]
    public void Validate_Should_Fail_When_OwnerUserIds_Is_Empty()
    {
        var sut = new AdminOptionsValidator();
        var options = Build(ownerUserIds: []);

        var result = sut.Validate(name: null, options: options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains(nameof(AdminOptions.OwnerUserIds)));
    }

    [Fact]
    public void Validate_Should_Succeed_When_All_Fields_Are_Valid()
    {
        var sut = new AdminOptionsValidator();
        var options = Build();

        var result = sut.Validate(name: null, options: options);

        result.Succeeded.Should().BeTrue();
        result.Failed.Should().BeFalse();
    }

    [Fact]
    public void Validate_Should_Accumulate_Multiple_Failures()
    {
        var sut = new AdminOptionsValidator();
        var options = new AdminOptions
        {
            GuildId = 0,
            LogsChannelId = 0,
            ErrorsChannelId = 0,
            OwnerUserIds = [],
        };

        var result = sut.Validate(name: null, options: options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().HaveCount(4);
    }
}
