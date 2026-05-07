using System.Collections.Immutable;
using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Contracts.Common;

namespace CharacterEngineDiscord.Contracts.Requests;

/// <summary>
/// Discord slash-command invocation forwarded from .DiscordBot to .Server.
/// Bot acks the interaction via DeferAsync BEFORE publishing this request,
/// so the InteractionToken stays valid in the 15-min followup window.
/// </summary>
public sealed record SlashCommandInvokedRequest : MessageEnvelope, IRequestMessage
{
    public required string CommandName { get; init; }
    public required ulong ApplicationId { get; init; }
    public required ulong GuildId { get; init; }
    public required ulong ChannelId { get; init; }
    public required ulong UserId { get; init; }
    public required string Username { get; init; }
    public required ulong InteractionId { get; init; }
    public required string InteractionToken { get; init; }
    public IReadOnlyDictionary<string, string> Options { get; init; } = ImmutableDictionary<string, string>.Empty;
}
