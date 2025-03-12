using CharacterEngineDiscord.Domain.Models.Db.Discord;
using CharacterEngineDiscord.Shared.Abstractions;

namespace CharacterEngineDiscord.Domain.Models.Abstractions;

public interface IGuildIntegration : IIntegration
{
    public Guid Id { get; set; }

    public ulong DiscordGuildId { get; set; }


    public string? GlobalMessagesFormat { get; set; }

    public DateTime CreatedAt { get; set; }

    public DiscordGuild DiscordGuild { get; set; }

    public bool IsChatOnly { get; }
}
