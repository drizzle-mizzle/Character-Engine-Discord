using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Messaging.Serialization;

namespace CharacterEngineDiscord.Messaging.Internals;

/// <summary>
/// Erased entry that knows how to register a single concrete message type with
/// <see cref="ICeMessageSerializer"/>. Closes the generic over the message type
/// so all entries can be enumerated and applied uniformly at startup.
/// </summary>
internal abstract class MessageTypeRegistration
{
    /// <summary>
    /// Calls <see cref="ICeMessageSerializer.Register{T}"/> with the captured message type.
    /// </summary>
    public abstract void RegisterTo(ICeMessageSerializer serializer);
}

/// <summary>
/// Concrete generic adapter that captures the message type for runtime registration.
/// </summary>
internal sealed class MessageTypeRegistration<TMessage> : MessageTypeRegistration
    where TMessage : IDomainMessage
{
    public override void RegisterTo(ICeMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        serializer.Register<TMessage>();
    }
}
