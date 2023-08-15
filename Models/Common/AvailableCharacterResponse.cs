namespace CharacterEngineDiscord.Models.Common
{
    internal class AvailableCharacterResponse
    {
        public required string? MessageId { get; set; }
        public required string? Text { get; set; }
        public required string? ImageUrl { get; set; }
        public required int TokensUsed { get; set; }
    }
}
