using CharacterEngineDiscord.Contracts.Abstractions;

namespace CharacterEngineDiscord.Messaging.Handlers;

/// <summary>
/// Implementations process a single concrete request type on the Server side.
/// Registered per-message-type in DI; resolved by <c>CeRequestDispatcher</c>.
/// </summary>
public interface ICeRequestHandler<in TRequest>
    where TRequest : IRequestMessage
{
    Task HandleAsync(TRequest request, CancellationToken cancellationToken);
}
