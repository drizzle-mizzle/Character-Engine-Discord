using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Contracts.Commands;
using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Core.Exceptions;
using CharacterEngineDiscord.Messaging.Abstractions;
using CharacterEngineDiscord.Server.RequestHandlers;
using CharacterEngineDiscord.Server.Routing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CharacterEngineDiscord.Tests.Server.Routing;

public sealed class CeSlashCommandRouterTests
{
    [Fact]
    public async Task HandleAsync_Should_Dispatch_Ping_To_PingHandler_For_CommandName_ping()
    {
        var publisher = new StubMessagePublisher();
        var ping = new StubbablePingHandler(publisher);
        var router = new CeSlashCommandRouter(ping, publisher, NullLogger<CeSlashCommandRouter>.Instance);

        var request = BuildRequest(commandName: "ping");

        await router.HandleAsync(request, CancellationToken.None);

        ping.InvocationCount.Should().Be(1);
        ping.LastRequest.Should().BeSameAs(request);
        publisher.PublishedCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_Skip_Unknown_Command_Without_Throw()
    {
        var publisher = new StubMessagePublisher();
        var ping = new StubbablePingHandler(publisher);
        var router = new CeSlashCommandRouter(ping, publisher, NullLogger<CeSlashCommandRouter>.Instance);

        var request = BuildRequest(commandName: "unknown");

        var act = async () => await router.HandleAsync(request, CancellationToken.None);

        await act.Should().NotThrowAsync();
        ping.InvocationCount.Should().Be(0);
        publisher.PublishedCommands.Should().BeEmpty();
        publisher.PublishedRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_Catch_UserFriendlyException_And_Publish_Ephemeral_Response()
    {
        const string userMessage = "you can't do this";

        var publisher = new StubMessagePublisher();
        var ping = new StubbablePingHandler(publisher)
        {
            StubBehavior = (_, _) => throw new UserFriendlyException(userMessage),
        };
        var router = new CeSlashCommandRouter(ping, publisher, NullLogger<CeSlashCommandRouter>.Instance);

        var request = BuildRequest(commandName: "ping");

        await router.HandleAsync(request, CancellationToken.None);

        publisher.PublishedCommands.Should().HaveCount(1);
        var published = publisher.PublishedCommands[0].Should().BeOfType<RespondToInteractionCommand>().Subject;

        published.Content.Should().Be(userMessage);
        published.IsEphemeral.Should().BeTrue();
        published.ApplicationId.Should().Be(request.ApplicationId);
        published.InteractionToken.Should().Be(request.InteractionToken);
        published.TraceId.Should().Be(request.TraceId);
        published.OriginGuildId.Should().Be(request.GuildId);
        published.OriginChannelId.Should().Be(request.ChannelId);
    }

    [Fact]
    public async Task HandleAsync_Should_Rethrow_NonUserFriendly_Exceptions()
    {
        var publisher = new StubMessagePublisher();
        var ping = new StubbablePingHandler(publisher)
        {
            StubBehavior = (_, _) => throw new InvalidOperationException("boom"),
        };
        var router = new CeSlashCommandRouter(ping, publisher, NullLogger<CeSlashCommandRouter>.Instance);

        var request = BuildRequest(commandName: "ping");

        var act = async () => await router.HandleAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        publisher.PublishedCommands.Should().BeEmpty();
    }

    private static SlashCommandInvokedRequest BuildRequest(string commandName)
    {
        return new SlashCommandInvokedRequest
        {
            TraceId = "abcdef12",
            MessageId = Guid.NewGuid(),
            OccurredAt = new DateTime(2026, 5, 7, 10, 30, 0, DateTimeKind.Utc),
            CommandName = commandName,
            ApplicationId = 1234567890123456789UL,
            GuildId = 100UL,
            ChannelId = 200UL,
            UserId = 300UL,
            Username = "tester",
            InteractionId = 999UL,
            InteractionToken = "interaction-token-abc",
        };
    }

    private sealed class StubMessagePublisher : ICeMessagePublisher
    {
        public List<ICommandMessage> PublishedCommands { get; } = new();
        public List<IRequestMessage> PublishedRequests { get; } = new();

        public Task PublishRequestAsync<TRequest>(TRequest message, CancellationToken cancellationToken = default)
            where TRequest : IRequestMessage
        {
            PublishedRequests.Add(message);
            return Task.CompletedTask;
        }

        public Task PublishCommandAsync<TCommand>(TCommand message, CancellationToken cancellationToken = default)
            where TCommand : ICommandMessage
        {
            PublishedCommands.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class StubbablePingHandler : PingSlashCommandHandler
    {
        public StubbablePingHandler(ICeMessagePublisher publisher)
            : base(publisher, NullLogger<PingSlashCommandHandler>.Instance)
        {
        }

        public Func<SlashCommandInvokedRequest, CancellationToken, Task>? StubBehavior { get; set; }

        public int InvocationCount { get; private set; }

        public SlashCommandInvokedRequest? LastRequest { get; private set; }

        public override async Task HandleAsync(SlashCommandInvokedRequest request, CancellationToken cancellationToken)
        {
            InvocationCount++;
            LastRequest = request;

            if (StubBehavior is not null)
            {
                await StubBehavior(request, cancellationToken);
                return;
            }

            // Default: do nothing — tests that don't override StubBehavior shouldn't
            // round-trip through the real publish path.
            await Task.CompletedTask;
        }
    }
}
