using CharacterEngineDiscord.Contracts.Abstractions;
using RabbitMQ.Client;

namespace CharacterEngineDiscord.Messaging.Serialization;

/// <summary>
/// Strategy for converting domain messages to/from AMQP byte payloads + properties.
/// Implementations are responsible for choosing the on-the-wire format and for
/// stamping AMQP metadata (type, correlation id, timestamp, etc.).
/// </summary>
public interface ICeMessageSerializer
{
    /// <summary>
    /// Serialise <paramref name="message"/> into an AMQP body and the matching
    /// <see cref="BasicProperties"/> bag for publication on <paramref name="channel"/>.
    /// </summary>
    (ReadOnlyMemory<byte> Body, BasicProperties Properties) Serialize<T>(IChannel channel, T message)
        where T : IDomainMessage;

    /// <summary>
    /// Reverse of <see cref="Serialize{T}"/>. Returns <c>null</c> when the AMQP <c>type</c>
    /// header is missing or refers to an unregistered CLR type — caller should reject without requeue.
    /// </summary>
    IDomainMessage? Deserialize(ReadOnlyMemory<byte> body, IReadOnlyBasicProperties properties);

    /// <summary>
    /// Register a concrete message type so it can be resolved on the consume side.
    /// Must be called once per type at startup, before any messages flow.
    /// </summary>
    void Register<T>() where T : IDomainMessage;
}
