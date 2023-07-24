namespace CharacterEngineDiscord.Models.Database
{
    public class Guild
    {
        public required ulong Id { get; set; }
        public required string? GuildCaiUserToken { get; set; }
        public required bool? GuildCaiPlusMode { get; set; }
        public required string? GuildOpenAiApiToken { get; set; }
        public required string? GuildOpenAiModel { get; set; }
        public required int BtnsRemoveDelay { get; set; }
        public virtual List<Channel> Channels { get; } = new();
    }
}
