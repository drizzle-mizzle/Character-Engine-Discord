namespace CharacterEngineDiscord.Contracts.Abstractions;

/// <summary>
/// Marker interface for messages flowing Bot -> Server (requests for the Server to act on).
/// Routed via the requests exchange, consumed by the Server.
/// </summary>
public interface IRequestMessage : IDomainMessage
{
}
