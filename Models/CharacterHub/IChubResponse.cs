namespace CharacterEngineDiscord.Models.CharacterHub
{
    public interface IChubResponse
    {
        int Code { get; }
        string? ErrorReason { get; }
        public bool IsSuccessful { get; }
        public bool IsFailure { get; }
    }
}
