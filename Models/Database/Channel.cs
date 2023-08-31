namespace CharacterEngineDiscord.Models.Database
{
    public class Channel
    {
        public ulong Id { get; set; }
        public required float RandomReplyChance { get; set; }
        //public string? ChannelJailbreakPrompt { get; set; } = null;
        //public string? ChannelMessagesFormat { get; set; } = null;

        public required ulong GuildId { get; set; }
        public virtual Guild Guild { get; set; } = null!;
        public virtual List<CharacterWebhook> CharacterWebhooks { get; } = new();
    }
}
