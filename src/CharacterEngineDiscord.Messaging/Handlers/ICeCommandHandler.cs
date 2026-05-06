using CharacterEngineDiscord.Contracts.Abstractions;

namespace CharacterEngineDiscord.Messaging.Handlers;

/// <summary>
/// Implementations process a single concrete command type on the Bot side.
/// Registered per-message-type in DI; resolved by <c>CeCommandDispatcher</c>.
/// </summary>
public interface ICeCommandHandler<in TCommand>
    where TCommand : ICommandMessage
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}
