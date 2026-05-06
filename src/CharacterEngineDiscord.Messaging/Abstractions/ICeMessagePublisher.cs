using CharacterEngineDiscord.Contracts.Abstractions;

namespace CharacterEngineDiscord.Messaging.Abstractions;

/// <summary>
/// Application-facing message bus surface. Use this from anywhere in DI to publish
/// requests (Bot -> Server) or commands (Server -> Bot). Implementation is a
/// process-wide singleton that owns a single AMQP channel under the hood.
/// </summary>
public interface ICeMessagePublisher
{
    Task PublishRequestAsync<TRequest>(TRequest message, CancellationToken cancellationToken = default)
        where TRequest : IRequestMessage;

    Task PublishCommandAsync<TCommand>(TCommand message, CancellationToken cancellationToken = default)
        where TCommand : ICommandMessage;
}
