namespace CharacterEngineDiscord.Models.Database
{
    public class Channel
    {
        public ulong Id { get; set; }
        public virtual required Guild Guild { get; set; }
        public virtual List<CharacterWebhook> CharacterWebhooks { get; } = new();
    }
}
