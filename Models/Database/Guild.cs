namespace CharacterEngineDiscord.Models.Database
{
    public class Guild
    {
        public required ulong Id { get; set; }
        public string? GuildMessagesFormat { get; set; }
        public string? GuildJailbreakPrompt { get; set; }

        // CharacterAI
        public string? GuildCaiUserToken { get; set; }
        public bool? GuildCaiPlusMode { get; set; }
        

        // OpenAI
        public string? GuildOpenAiApiEndpoint { get; set; }
        public string? GuildOpenAiApiToken { get; set; }
        public string? GuildOpenAiModel { get; set; }

        // KoboldAI
        public string? GuildKoboldAiApiEndpoint { get; set; }

        // Horde
        public string? GuildHordeApiToken { get; set; }
        public string? GuildHordeModel { get; set; }


        public required int MessagesSent { get; set; }

        public virtual List<Channel> Channels { get; } = [];
        public virtual List<BlockedUser> BlockedUsers { get; } = [];
    }
}
