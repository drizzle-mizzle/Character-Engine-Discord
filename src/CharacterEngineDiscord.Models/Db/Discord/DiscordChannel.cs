namespace CharacterEngineDiscord.Db.Models.Db.Discord;


public class DiscordChannel
{
    public required ulong Id { get; set; }

    public required string ChannelName { get; set; }

    public required ulong DiscordGuildId { get; set; }
}
