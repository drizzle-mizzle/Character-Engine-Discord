using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Messaging.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.Messaging.Internals;

/// <summary>
/// Resolves the <see cref="ICeCommandHandler{TCommand}"/> matching the runtime type of an
/// incoming <see cref="ICommandMessage"/> from a fresh DI scope and invokes it.
/// </summary>
internal sealed class CeCommandDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CeCommandDispatcher> _logger;

    public CeCommandDispatcher(IServiceScopeFactory scopeFactory, ILogger<CeCommandDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task DispatchAsync(ICommandMessage message, CancellationToken cancellationToken)
    {
        var runtimeType = message.GetType();
        var handlerType = typeof(ICeCommandHandler<>).MakeGenericType(runtimeType);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetService(handlerType);

        if (handler is null)
        {
            _logger.LogWarning(
                "[{Trace}] No command handler registered for {Type}; message acknowledged without action",
                message.TraceId, runtimeType.Name);
            return;
        }

        var method = handlerType.GetMethod(nameof(ICeCommandHandler<ICommandMessage>.HandleAsync))
                     ?? throw new InvalidOperationException($"Handler {handlerType} missing HandleAsync");

        var task = (Task)method.Invoke(handler, [message, cancellationToken])!;
        await task;
    }
}
