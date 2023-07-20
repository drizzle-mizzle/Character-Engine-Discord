namespace CharacterEngineDiscord.Models.Database
{
    public class History
    {
        public required string Id { get; set; }
        public required bool IsActive { get; set; }
        public required DateTime CreatedAt { get; set; }
        public virtual required CharacterWebhook CharacterWebhook { get; set; }
    }
}
