using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Contracts.Common;

namespace CharacterEngineDiscord.Contracts.Commands;

/// <summary>
/// Tells .DiscordBot to send a followup message for an interaction the bot
/// previously deferred. Issued by .Server in response to a SlashCommandInvokedRequest
/// (or any other interaction request).
/// </summary>
public sealed record RespondToInteractionCommand : MessageEnvelope, ICommandMessage
{
    public required ulong ApplicationId { get; init; }
    public required string InteractionToken { get; init; }
    public required string Content { get; init; }
    public bool IsEphemeral { get; init; }
    public ulong? OriginGuildId { get; init; }
    public ulong? OriginChannelId { get; init; }
}
