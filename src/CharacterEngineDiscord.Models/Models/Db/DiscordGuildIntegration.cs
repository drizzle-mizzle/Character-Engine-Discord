namespace CharacterEngineDiscord.Models.Db;


public class DiscordGuildIntegration
{
    public required Guid Id { get; set; }
    public required ulong DiscordGuildId { get; set; }
    public required Guid GuildIntegraionId { get; set; }
    public required IntegrationType IntegrationType { get; set; }
}
