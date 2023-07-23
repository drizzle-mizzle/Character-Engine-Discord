namespace CharacterEngineDiscord.Models.Database
{
    public class Channel
    {
        public ulong Id { get; set; }
        public required ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; } = null!;
        public virtual List<CharacterWebhook> CharacterWebhooks { get; } = new();
    }
}
