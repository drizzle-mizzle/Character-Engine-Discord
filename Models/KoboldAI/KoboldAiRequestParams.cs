namespace CharacterEngineDiscord.Models.KoboldAI
{
    public class KoboldAiRequestParams
    {
        public required string ApiEndpoint { get; set; }
        public required int MaxContextLength { get; set; }
        public required int MaxLength { get; set; }
        public required float Temperature { get; set; }
        public required float RepetitionPenalty { get; set; }
        public required float RepetitionPenaltySlope { get; set; }
        public required float TopP { get; set; }
        public required float TopA { get; set; }
        public required int TopK { get; set; }
        public required float TypicalSampling { get; set; }
        public required float TailFreeSampling { get; set; }
        public required bool SingleLine { get; set; }
        public required List<KoboldAiMessage> Messages { get; set; }
    }

    public readonly struct KoboldAiMessage
    {
        public string Role { get; }
        public string Content { get; }

        public KoboldAiMessage(string role, string content)
        {
            Content = content;
            Role = role;
        }
    }
}
