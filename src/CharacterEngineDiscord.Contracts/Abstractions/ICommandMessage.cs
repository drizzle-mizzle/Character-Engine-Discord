namespace CharacterEngineDiscord.Contracts.Abstractions;

/// <summary>
/// Marker interface for messages flowing Server -> Bot (commands telling the Bot to perform a Discord action).
/// Routed via the commands exchange, consumed by the Bot.
/// </summary>
public interface ICommandMessage : IDomainMessage
{
}
