using RabbitMQ.Client;

namespace CharacterEngineDiscord.Messaging.Topology;

/// <summary>
/// Idempotently declares the exchanges, queues, and bindings required by the
/// Character Engine messaging stack. Invoked once per process at startup.
/// </summary>
public interface ICeRabbitTopology
{
    Task EnsureCreatedAsync(IChannel channel, CancellationToken cancellationToken);
}
