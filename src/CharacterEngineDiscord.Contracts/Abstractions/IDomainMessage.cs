namespace CharacterEngineDiscord.Contracts.Abstractions;

/// <summary>
/// Common base for any message that flows through the message bus.
/// Provides the metadata required for cross-process correlation, idempotency, and versioning.
/// </summary>
public interface IDomainMessage
{
    /// <summary>Short correlation id propagated across process boundaries (matches <c>CharacterEngineDiscord.Core.Helpers.TraceId</c>).</summary>
    string TraceId { get; }

    /// <summary>Unique identifier for this individual message instance; used for idempotency and dedup.</summary>
    Guid MessageId { get; }

    /// <summary>UTC instant the originating side produced the message.</summary>
    DateTime OccurredAt { get; }

    /// <summary>Schema version of the message payload; increment on breaking changes.</summary>
    int MessageVersion { get; }
}
