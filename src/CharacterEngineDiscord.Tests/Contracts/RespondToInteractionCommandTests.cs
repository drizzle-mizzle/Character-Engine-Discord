using CharacterEngineDiscord.Contracts.Commands;
using CharacterEngineDiscord.Messaging.Serialization;
using FluentAssertions;

namespace CharacterEngineDiscord.Tests.Contracts;

public sealed class RespondToInteractionCommandTests
{
    [Fact]
    public void RoundTrip_Should_Preserve_All_Required_Fields()
    {
        var serializer = new CeJsonMessageSerializer();
        serializer.Register<RespondToInteractionCommand>();

        var original = new RespondToInteractionCommand
        {
            TraceId = "12345678",
            MessageId = Guid.NewGuid(),
            OccurredAt = new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc),
            ApplicationId = 9876543210123456UL,
            InteractionToken = "long-interaction-token-xyz",
            Content = "<@555> Pong!",
            IsEphemeral = true,
            OriginGuildId = 42UL,
            OriginChannelId = 84UL,
        };

        var (body, props) = serializer.Serialize(original);

        var deserialized = serializer.Deserialize(body, props);

        deserialized.Should().NotBeNull();
        var typed = deserialized.Should().BeOfType<RespondToInteractionCommand>().Subject;

        typed.TraceId.Should().Be(original.TraceId);
        typed.MessageId.Should().Be(original.MessageId);
        typed.OccurredAt.Should().Be(original.OccurredAt);
        typed.MessageVersion.Should().Be(original.MessageVersion);
        typed.ApplicationId.Should().Be(original.ApplicationId);
        typed.InteractionToken.Should().Be(original.InteractionToken);
        typed.Content.Should().Be(original.Content);
        typed.IsEphemeral.Should().Be(original.IsEphemeral);
        typed.OriginGuildId.Should().Be(original.OriginGuildId);
        typed.OriginChannelId.Should().Be(original.OriginChannelId);
    }

    [Fact]
    public void RoundTrip_Should_Preserve_Null_Optional_Origins()
    {
        var serializer = new CeJsonMessageSerializer();
        serializer.Register<RespondToInteractionCommand>();

        var original = new RespondToInteractionCommand
        {
            TraceId = "00000000",
            MessageId = Guid.NewGuid(),
            OccurredAt = new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc),
            ApplicationId = 1UL,
            InteractionToken = "t",
            Content = "ok",
            IsEphemeral = false,
        };

        var (body, props) = serializer.Serialize(original);

        var deserialized = serializer.Deserialize(body, props);

        var typed = deserialized.Should().BeOfType<RespondToInteractionCommand>().Subject;
        typed.OriginGuildId.Should().BeNull();
        typed.OriginChannelId.Should().BeNull();
        typed.IsEphemeral.Should().BeFalse();
    }
}
