namespace CharacterEngineDiscord.Models.Database
{
    public class Guild
    {
        public required ulong Id { get; set; }
        public string? DefaultUserToken { get; set; }
        public virtual List<Channel> Channels { get; } = new();
    }
}
