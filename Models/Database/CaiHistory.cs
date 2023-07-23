namespace CharacterEngineDiscord.Models.Database
{
    public class CaiHistory
    {
        public required string Id { get; set; }
        public required bool IsActive { get; set; }
        public required DateTime CreatedAt { get; set; }
        public required ulong CharacterWebhookId { get; set; }
        public virtual CharacterWebhook CharacterWebhook { get; set; } = null!;
    }
}
