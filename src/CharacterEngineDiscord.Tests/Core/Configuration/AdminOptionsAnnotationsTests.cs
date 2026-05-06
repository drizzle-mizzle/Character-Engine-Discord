using System.ComponentModel.DataAnnotations;
using CharacterEngineDiscord.Core.Configuration;
using FluentAssertions;

namespace CharacterEngineDiscord.Tests.Core.Configuration;

public sealed class AdminOptionsAnnotationsTests
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

    private static (bool ok, List<ValidationResult> results) Run(AdminOptions options)
    {
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);
        return (ok, results);
    }

    [Fact]
    public void TryValidateObject_Should_Fail_When_GuildId_Is_Zero()
    {
        var (ok, results) = Run(Build(guildId: 0ul));

        ok.Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(AdminOptions.GuildId)));
    }

    [Fact]
    public void TryValidateObject_Should_Fail_When_LogsChannelId_Is_Zero()
    {
        var (ok, results) = Run(Build(logsChannelId: 0ul));

        ok.Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(AdminOptions.LogsChannelId)));
    }

    [Fact]
    public void TryValidateObject_Should_Fail_When_ErrorsChannelId_Is_Zero()
    {
        var (ok, results) = Run(Build(errorsChannelId: 0ul));

        ok.Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(AdminOptions.ErrorsChannelId)));
    }

    [Fact]
    public void TryValidateObject_Should_Fail_When_OwnerUserIds_Is_Empty()
    {
        var (ok, results) = Run(Build(ownerUserIds: []));

        ok.Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(AdminOptions.OwnerUserIds)));
    }

    [Fact]
    public void TryValidateObject_Should_Succeed_When_All_Fields_Are_Valid()
    {
        var (ok, results) = Run(Build());

        ok.Should().BeTrue();
        results.Should().BeEmpty();
    }
}
