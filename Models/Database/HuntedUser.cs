namespace CharacterEngineDiscord.Models.Database
{
    public class HuntedUser
    {
        public int Id { get; set; }
        public required ulong UserId { get; set; }
        public required float Chance { get; set; }
        public required ulong CharacterWebhookId { get; set; }
        public virtual CharacterWebhook CharacterWebhook { get; set; } = null!;
    }
}
