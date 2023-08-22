namespace CharacterEngineDiscord.Models.Database
{
    public class Guild
    {
        public required ulong Id { get; set; }
        public string? GuildMessagesFormat { get; set; } = null;
        public string? GuildJailbreakPrompt { get; set; } = null;

        public string? GuildCaiUserToken { get; set; } = null;
        public bool? GuildCaiPlusMode { get; set; } = null;

        public string? GuildOpenAiApiEndpoint { get; set; } = null;
        public string? GuildOpenAiApiToken { get; set; } = null;
        public string? GuildOpenAiModel { get; set; } = null;
        public virtual List<Channel> Channels { get; } = new();
        public virtual List<BlockedUser> BlockedUsers { get; } = new();
    }
}
