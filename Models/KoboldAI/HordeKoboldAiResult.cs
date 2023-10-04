namespace CharacterEngineDiscord.Models.KoboldAI
{
    internal class HordeKoboldAiResult
    {
        public KoboldAiMessage? Message { get; set; }
        public bool IsSuccessful { get; set; }
        public string? ErrorReason { get; set; }
    }
}
