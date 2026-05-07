using CharacterEngineDiscord.Contracts.Abstractions;
using CharacterEngineDiscord.Contracts.Common;

namespace CharacterEngineDiscord.Contracts.Commands;

/// <summary>
/// Tells .DiscordBot to post a plain-text message into a pre-resolved admin channel.
/// The publisher (Bot or Server) chooses <see cref="TargetChannelId"/> from
/// <c>AdminOptions</c> according to severity; the handler is a dumb executor that
/// just performs <c>SendMessageAsync</c>.
/// </summary>
public sealed record ReportLogToAdminChannelCommand : MessageEnvelope, ICommandMessage
{
    /// <summary>Already-resolved Discord channel id (logs channel or errors channel).</summary>
    public required ulong TargetChannelId { get; init; }

    /// <summary>Pre-formatted plain-text payload (already trimmed to the Discord 2000-char limit).</summary>
    public required string Content { get; init; }

    /// <summary>True when the original severity was <c>Error</c> or <c>Critical</c>; for handler-side logging only.</summary>
    public required bool IsError { get; init; }
}
