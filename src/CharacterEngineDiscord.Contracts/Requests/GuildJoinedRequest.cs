using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Contracts.Common;

namespace CharacterEngineDiscord.Contracts.Requests;

/// <summary>
/// Discord <c>JoinedGuild</c> event forwarded from .DiscordBot to .Server for persistence.
/// The publisher (.DiscordBot) snapshots gateway-side state at the moment of the event;
/// the consumer (.Server) is responsible for inserting / refreshing / resurrecting the row.
/// </summary>
public sealed record GuildJoinedRequest : MessageEnvelope, IRequestMessage
{
    public required ulong GuildId { get; init; }
    public required string Name { get; init; }
    public required ulong OwnerId { get; init; }
    public string? OwnerUsername { get; init; }
    public required int MemberCount { get; init; }
    public string? IconUrl { get; init; }
}
