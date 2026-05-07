using System.Collections.Immutable;
using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Messaging.Serialization;
using FluentAssertions;

namespace CharacterEngineDiscord.Tests.Contracts;

public sealed class SlashCommandInvokedRequestTests
{
    [Fact]
    public void RoundTrip_Should_Preserve_All_Required_Fields()
    {
        var serializer = new CeJsonMessageSerializer();
        serializer.Register<SlashCommandInvokedRequest>();

        var original = new SlashCommandInvokedRequest
        {
            TraceId = "abcdef12",
            MessageId = Guid.NewGuid(),
            OccurredAt = new DateTime(2026, 5, 7, 10, 30, 0, DateTimeKind.Utc),
            CommandName = "ping",
            ApplicationId = 1234567890123456789UL,
            GuildId = 100UL,
            ChannelId = 200UL,
            UserId = 300UL,
            Username = "tester",
            InteractionId = 999UL,
            InteractionToken = "interaction-token-abc",
            Options = new Dictionary<string, string>
            {
                ["foo"] = "bar",
                ["count"] = "42",
            }.ToImmutableDictionary(),
        };

        var (body, props) = serializer.Serialize(original);

        var deserialized = serializer.Deserialize(body, props);

        deserialized.Should().NotBeNull();
        var typed = deserialized.Should().BeOfType<SlashCommandInvokedRequest>().Subject;

        typed.TraceId.Should().Be(original.TraceId);
        typed.MessageId.Should().Be(original.MessageId);
        typed.OccurredAt.Should().Be(original.OccurredAt);
        typed.MessageVersion.Should().Be(original.MessageVersion);
        typed.CommandName.Should().Be(original.CommandName);
        typed.ApplicationId.Should().Be(original.ApplicationId);
        typed.GuildId.Should().Be(original.GuildId);
        typed.ChannelId.Should().Be(original.ChannelId);
        typed.UserId.Should().Be(original.UserId);
        typed.Username.Should().Be(original.Username);
        typed.InteractionId.Should().Be(original.InteractionId);
        typed.InteractionToken.Should().Be(original.InteractionToken);
        typed.Options.Should().BeEquivalentTo(original.Options);
    }

    [Fact]
    public void Deserialize_Should_Return_Null_When_Type_Is_Not_Registered()
    {
        var serializer = new CeJsonMessageSerializer();
        // Intentionally NOT registering SlashCommandInvokedRequest.

        var original = new SlashCommandInvokedRequest
        {
            TraceId = "deadbeef",
            MessageId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            CommandName = "ping",
            ApplicationId = 1UL,
            GuildId = 1UL,
            ChannelId = 1UL,
            UserId = 1UL,
            Username = "u",
            InteractionId = 1UL,
            InteractionToken = "t",
        };

        var (body, props) = serializer.Serialize(original);

        var result = serializer.Deserialize(body, props);

        result.Should().BeNull();
    }
}
