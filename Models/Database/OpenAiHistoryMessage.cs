namespace CharacterEngineDiscord.Models.Database
{
    public class OpenAiHistoryMessage
    {
        public ulong Id { get; set; }
        public required string Role { get; set; }
        public required string Content { get; set; }
        public required ulong CharacterWebhookId { get; set; }
        public virtual CharacterWebhook CharacterWebhook { get; set; } = null!;
    }
}
