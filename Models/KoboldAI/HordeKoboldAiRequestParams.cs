namespace CharacterEngineDiscord.Models.KoboldAI
{
    public class HordeKoboldAiRequestParams
    {
        public required string Token { get; set; }
        public required string Model { get; set; }
        public required KoboldAiRequestParams KoboldAiSettings { get; set; }
    }
}
