using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Messaging.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.Messaging.Internals;

/// <summary>
/// Resolves the <see cref="ICeRequestHandler{TRequest}"/> matching the runtime type of an
/// incoming <see cref="IRequestMessage"/> from a fresh DI scope and invokes it.
/// Unhandled types log a warning and ack — they MUST NOT throw, otherwise the consumer
/// requeues forever.
/// </summary>
internal sealed class CeRequestDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CeRequestDispatcher> _logger;

    public CeRequestDispatcher(IServiceScopeFactory scopeFactory, ILogger<CeRequestDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task DispatchAsync(IRequestMessage message, CancellationToken cancellationToken)
    {
        var runtimeType = message.GetType();
        var handlerType = typeof(ICeRequestHandler<>).MakeGenericType(runtimeType);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetService(handlerType);

        if (handler is null)
        {
            _logger.LogWarning(
                "[{Trace}] No request handler registered for {Type}; message acknowledged without action",
                message.TraceId, runtimeType.Name);
            return;
        }

        var method = handlerType.GetMethod(nameof(ICeRequestHandler<IRequestMessage>.HandleAsync))
                     ?? throw new InvalidOperationException($"Handler {handlerType} missing HandleAsync");

        var task = (Task)method.Invoke(handler, [message, cancellationToken])!;
        await task;
    }
}
