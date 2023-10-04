namespace CharacterEngineDiscord.Models.KoboldAI
{
    public interface IKoboldAiResponse
    {
        int Code { get; }
        string? ErrorReason { get; }
        public bool IsSuccessful { get; }
        public bool IsFailure { get; }
    }
}
