namespace CharacterEngineDiscord.Services
{
    public class KoboldAiIntegration
    {


    }

    public class KoboldAiChatPayload
    {
        public required string Prompt { get; set; }
        public required float Temperature { get; set; }


        public required int MaxContextLength { get; set; }
        public required int MaxLength { get; set; }
        public required float RepetitionPenalty { get; set; }
        public required float RepetitionPenaltyRange { get; set; }

    }
}
