using CharacterEngineDiscord.Contracts.Abstractions;

namespace CharacterEngineDiscord.Contracts.Common;

/// <summary>
/// Abstract record base providing the standard envelope fields for every concrete message type.
/// Concrete <see cref="IRequestMessage"/> / <see cref="ICommandMessage"/> records inherit this
/// to gain the trace, identity, timestamp, and versioning surface.
/// </summary>
public abstract record MessageEnvelope : IDomainMessage
{
    /// <inheritdoc />
    public required string TraceId { get; init; }

    /// <inheritdoc />
    public required Guid MessageId { get; init; }

    /// <inheritdoc />
    public required DateTime OccurredAt { get; init; }

    /// <inheritdoc />
    public int MessageVersion { get; init; } = 1;
}
