using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Contracts.Common;

namespace CharacterEngineDiscord.Contracts.Requests;

/// <summary>
/// Discord <c>LeftGuild</c> event forwarded from .DiscordBot to .Server for soft-deletion.
/// </summary>
public sealed record GuildLeftRequest : MessageEnvelope, IRequestMessage
{
    public required ulong GuildId { get; init; }
    public required string Name { get; init; }
}
