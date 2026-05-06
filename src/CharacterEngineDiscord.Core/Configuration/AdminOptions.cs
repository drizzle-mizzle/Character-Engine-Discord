using System.ComponentModel.DataAnnotations;

namespace CharacterEngineDiscord.Core.Configuration;

/// <summary>
/// Identifiers for the operator's home guild plus the channels that receive
/// bot logs and error reports.
/// Bound from configuration section <c>Admin</c>.
/// </summary>
public sealed class AdminOptions
{
    [Required]
    [Range(1, ulong.MaxValue)]
    public ulong GuildId { get; init; }

    public string? InviteLink { get; init; }

    [Range(1, ulong.MaxValue)]
    public ulong LogsChannelId { get; init; }

    [Range(1, ulong.MaxValue)]
    public ulong ErrorsChannelId { get; init; }

    [Required]
    [MinLength(1)]
    public ulong[] OwnerUserIds { get; init; } = [];
}
