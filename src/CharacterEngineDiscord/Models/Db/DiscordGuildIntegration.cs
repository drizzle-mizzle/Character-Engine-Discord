namespace CharacterEngine.Models.Db;


public class DiscordGuildIntegration
{
    public required Guid Id { get; set; }
    public required ulong DiscordGuildId { get; set; }
    public required Guid GuildIntegraionId { get; set; }
    public required Enums.IntegrationType IntegrationType { get; set; }
}
